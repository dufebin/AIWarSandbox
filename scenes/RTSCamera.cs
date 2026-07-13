using System.Collections.Generic;
using Godot;

namespace AIWarSandbox.Scenes;

public partial class RTSCamera : Camera3D
{
    private float _panSpeed = 0.6f;
    private float _zoomSpeed = 0.012f;
    private float _keyMoveSpeed = 40f;
    private float _keyZoomSpeed = 30f;
    private float _minY = 12f;
    private float _maxY = 90f;
    private float _edgeMargin = 18f;
    private float _edgeSpeed = 35f;
    private float _yaw;
    private float _pitch = Mathf.DegToRad(-45f);
    private bool _mmbDragging;

    private readonly Dictionary<int, Vector2> _touches = new();
    private float _lastPinchDist = -1f;

    public override void _Ready()
    {
        _yaw = Rotation.Y;
        _pitch = Rotation.X;
    }

    public void FocusOn(Vector3 worldPos)
    {
        GlobalPosition = new Vector3(worldPos.X, GlobalPosition.Y, worldPos.Z + GlobalPosition.Y * 0.7f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var dir = Vector3.Zero;

        if (Input.IsActionPressed("move_forward")) dir.Z -= 1;
        if (Input.IsActionPressed("move_back")) dir.Z += 1;
        if (Input.IsActionPressed("move_left")) dir.X -= 1;
        if (Input.IsActionPressed("move_right")) dir.X += 1;

        var mouse = GetViewport().GetMousePosition();
        var size = GetViewport().GetVisibleRect().Size;
        bool edge = false;
        if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X <= size.X && mouse.Y <= size.Y)
        {
            if (mouse.X < _edgeMargin) { dir.X -= 1; edge = true; }
            if (mouse.X > size.X - _edgeMargin) { dir.X += 1; edge = true; }
            if (mouse.Y < _edgeMargin) { dir.Z -= 1; edge = true; }
            if (mouse.Y > size.Y - _edgeMargin) { dir.Z += 1; edge = true; }
        }

        if (dir != Vector3.Zero)
        {
            dir = dir.Normalized();
            float cos = Mathf.Cos(_yaw), sin = Mathf.Sin(_yaw);
            var world = new Vector3(dir.X * cos - dir.Z * sin, 0, dir.X * sin + dir.Z * cos);
            float speed = edge ? _edgeSpeed : _keyMoveSpeed;
            GlobalPosition += world * speed * dt;
        }

        if (Input.IsKeyPressed(Key.Q)) { _yaw += 1.2f * dt; ApplyRotation(); }
        if (Input.IsKeyPressed(Key.E)) { _yaw -= 1.2f * dt; ApplyRotation(); }

        if (Input.IsKeyPressed(Key.KpAdd) || Input.IsKeyPressed(Key.Equal))
            GlobalPosition -= new Vector3(0, _keyZoomSpeed * dt, 0);
        if (Input.IsKeyPressed(Key.KpSubtract) || Input.IsKeyPressed(Key.Minus))
            GlobalPosition += new Vector3(0, _keyZoomSpeed * dt, 0);

        ClampY();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            { GlobalPosition -= new Vector3(0, 3f, 0); ClampY(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            { GlobalPosition += new Vector3(0, 3f, 0); ClampY(); }
            else if (mb.ButtonIndex == MouseButton.Middle)
                _mmbDragging = mb.Pressed;
        }
        else if (ev is InputEventMouseMotion mm && _mmbDragging)
        {
            var pan = -mm.Relative * _panSpeed * (GlobalPosition.Y / 30f);
            float cos = Mathf.Cos(_yaw), sin = Mathf.Sin(_yaw);
            GlobalPosition += new Vector3(pan.X * cos - pan.Y * sin, 0, pan.X * sin + pan.Y * cos);
        }
        else if (ev is InputEventScreenTouch touch && touch.Pressed)
        {
            _touches[touch.Index] = touch.Position;
            if (_touches.Count == 2) _lastPinchDist = CurrentPinchDistance();
        }
        else if (ev is InputEventScreenTouch touchUp && !touchUp.Pressed)
        {
            _touches.Remove(touchUp.Index);
            if (_touches.Count < 2) _lastPinchDist = -1f;
        }
        else if (ev is InputEventScreenDrag drag)
        {
            _touches[drag.Index] = drag.Position;
            if (_touches.Count == 1)
            {
                var pan = -drag.Relative * _panSpeed * (GlobalPosition.Y / 30f);
                GlobalPosition += new Vector3(pan.X, 0, pan.Y);
            }
            else if (_touches.Count >= 2)
            {
                float curDist = CurrentPinchDistance();
                if (_lastPinchDist > 0f)
                {
                    GlobalPosition -= new Vector3(0, (curDist - _lastPinchDist) * _zoomSpeed * 30f, 0);
                    ClampY();
                }
                _lastPinchDist = curDist;
            }
        }
    }

    private void ApplyRotation() => Rotation = new Vector3(_pitch, _yaw, 0);

    private float CurrentPinchDistance()
    {
        var keys = new List<int>(_touches.Keys);
        if (keys.Count < 2) return -1f;
        return _touches[keys[0]].DistanceTo(_touches[keys[1]]);
    }

    private void ClampY()
    {
        if (GlobalPosition.Y < _minY)
            GlobalPosition = new Vector3(GlobalPosition.X, _minY, GlobalPosition.Z);
        if (GlobalPosition.Y > _maxY)
            GlobalPosition = new Vector3(GlobalPosition.X, _maxY, GlobalPosition.Z);
    }
}
