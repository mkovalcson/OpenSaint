// ---------------------------------------------------------------------------
// RgbCommandWireFormat.cs
//
// The editor/timeline stores RGB command arguments in the natural, readable
// Red,Green,Blue order. ArduinoOpenSaintRGB.ino expects the first two color
// arguments on the serial wire in Green,Red,Blue order; its parser then calls
// Color(token[2], token[1], token[3]). Keep that transport quirk isolated here
// so saved sequences and the RGB builder remain normal RGB.
// ---------------------------------------------------------------------------

namespace ServoAnimator
{
    internal static class RgbCommandWireFormat
    {
        private static readonly HashSet<string> ColorCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "SETRGBCOLOR",
            "COLORWIPEEYES",
            "FADE",
            "PULSE",
            "THEATERCHASE",
        };

        /// <summary>
        /// Convert an editor RGB command from Red,Green,Blue argument order to
        /// the Green,Red,Blue order expected on the Arduino serial wire.
        /// Non-color commands are returned unchanged.
        /// </summary>
        public static string ToArduinoWireOrder(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return commandText ?? "";

            string[] parts = commandText.Split(',');
            if (parts.Length < 4 || !ColorCommands.Contains(parts[0].Trim()))
                return commandText;

            // Editor:  command, RED, GREEN, BLUE, ...
            // Arduino: command, GREEN, RED, BLUE, ...
            (parts[1], parts[2]) = (parts[2], parts[1]);
            return string.Join(",", parts);
        }
    }
}
