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

    private readonly Dictionary<int, Vector2> _touches = new();
    private float _lastPinchDist = -1f;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var dir = Vector3.Zero;

        if (Input.IsActionPressed("move_forward")) dir.Z -= 1;
        if (Input.IsActionPressed("move_back")) dir.Z += 1;
        if (Input.IsActionPressed("move_left")) dir.X -= 1;
        if (Input.IsActionPressed("move_right")) dir.X += 1;

        if (dir != Vector3.Zero)
        {
            dir = dir.Normalized();
            GlobalPosition += new Vector3(dir.X, 0, dir.Z) * _keyMoveSpeed * dt;
        }

        if (Input.IsKeyPressed(Key.KpAdd) || Input.IsKeyPressed(Key.Equal))
            GlobalPosition -= new Vector3(0, _keyZoomSpeed * dt, 0);
        if (Input.IsKeyPressed(Key.KpSubtract) || Input.IsKeyPressed(Key.Minus))
            GlobalPosition += new Vector3(0, _keyZoomSpeed * dt, 0);

        ClampY();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventScreenTouch touch && touch.Pressed)
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
                    float delta = curDist - _lastPinchDist;
                    GlobalPosition -= new Vector3(0, delta * _zoomSpeed * 30f, 0);
                    ClampY();
                }
                _lastPinchDist = curDist;
            }
        }
    }

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
