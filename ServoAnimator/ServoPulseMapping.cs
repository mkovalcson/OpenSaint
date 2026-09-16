namespace ServoAnimator;

internal static class ServoPulseMapping
{
    internal static double ToPulse(ServoConfiguration config, ServoNames parent, ServoConfigEntry entry, double value)
    {
        bool centered = ServoCommand.RangeFor(parent).Min < 0;
        double pulse;
        if (centered)
        {
            if (config.GangReversed(parent, entry.Control)) value = -value;
            if (entry.Reversed) value = -value;
            pulse = entry.DefaultPwm + value / 100 * (value < 0 ? entry.DefaultPwm - entry.MinPwm : entry.MaxPwm - entry.DefaultPwm);
        }
        else pulse = entry.Reversed ? entry.MaxPwm - value / 100 * (entry.MaxPwm - entry.MinPwm) : entry.DefaultPwm + value / 100 * (entry.MaxPwm - entry.MinPwm);
        return Math.Clamp(pulse, entry.MinPwm, entry.MaxPwm);
    }
    internal static double ToLogical(ServoConfiguration config, ServoNames parent, ServoConfigEntry entry, double pulse)
    {
        double value;
        if (ServoCommand.RangeFor(parent).Min < 0)
        {
            double difference = pulse - entry.DefaultPwm;
            value = difference * 100 / Math.Max(1, difference < 0 ? entry.DefaultPwm - entry.MinPwm : entry.MaxPwm - entry.DefaultPwm);
            if (entry.Reversed) value = -value;
            if (config.GangReversed(parent, entry.Control)) value = -value;
        }
        else value = (entry.Reversed ? entry.MaxPwm - pulse : pulse - entry.DefaultPwm) * 100 / Math.Max(1, entry.MaxPwm - entry.MinPwm);
        var range = ServoCommand.RangeFor(parent); return Math.Clamp(value, range.Min, range.Max);
    }
}
