namespace ATSTelemetry;

/// <summary>
/// Valor animado con resorte críticamente amortiguado. Se usa para suavizar
/// cambios en vivo (velocidad, RPM, barras) sin saltos bruscos.
/// </summary>
internal sealed class AnimatedValue
{
    private double _value;
    private double _velocity;
    private bool _initialized;

    public double Value => _value;

    public void Update(double target, double dt)
    {
        if (!_initialized)
        {
            _value = target;
            _velocity = 0;
            _initialized = true;
            return;
        }

        const double stiffness = 120.0;
        const double damping = 20.0;

        var force = (target - _value) * stiffness - _velocity * damping;
        _velocity += force * dt;
        _value += _velocity * dt;

        if (Math.Abs(target - _value) < 0.001 && Math.Abs(_velocity) < 0.001)
        {
            _value = target;
            _velocity = 0;
        }
    }

    public void Snap(double target)
    {
        _value = target;
        _velocity = 0;
        _initialized = true;
    }

    public void Reset()
    {
        _initialized = false;
        _value = 0;
        _velocity = 0;
    }
}