namespace ServoAnimator
{
    internal static class MoviePoseContinuity
    {
        // An untouched child keeps its individual carried pose, not the gang's
        // default value. Use the same precedence for capture and display.
        internal static double ChildValue(double gangValue, double? gangTime,
            ServoCommand child, double? carried)
        {
            if (child != null && (!gangTime.HasValue || child.OffsetSeconds > gangTime.Value))
                return child.NumericValue;
            if (!gangTime.HasValue && child == null && carried.HasValue)
                return carried.Value;
            return gangValue;
        }
    }
}
