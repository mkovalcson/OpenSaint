using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TicT249Control
{ 
 
    public class TicController
    {
        // default installed location, overwritten on initialization
        public string TicCmdPathName = "C:\\Program Files (x86)\\Pololu\\Tic\\bin\\ticcmd"; 

        public RobotControls Name;
        public string SerialNumber;     

        public int minValue;
        public int maxValue;
        public int CurrentValue;

        /// <summary>
        /// Initializes each Tic 249 with the acceleration deceleration etc values in the settings file.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="exeFolder"></param>
        /// <param name="controlname"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        public TicController(string serialNumber, string exeFolder, RobotControls controlname, int minValue, int maxValue)
        {
            Name = controlname;
            SerialNumber = serialNumber;
            TicCmdPathName = exeFolder + "TIC\\ticcmd";
            var configPathName = exeFolder + "TIC\\tic_settings.txt";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = TicCmdPathName,
                    Arguments = $"--serial {SerialNumber} --settings \"{configPathName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[ERROR] {error}");
            }
            else
            {
                Console.WriteLine($"{output} Stepper {SerialNumber} load config file.");
            }

            this.minValue = minValue;
            this.maxValue = maxValue;
        }

        public void MoveMin()
        {
            MoveToPosition(minValue);
        }
        public void MoveMax()
        {
            MoveToPosition(maxValue);
        }


        public void MoveToPosition(int position)
        {
            CurrentValue = position;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = TicCmdPathName,
                    Arguments = $"--serial {SerialNumber} --exit-safe-start --position {position}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            this.CurrentValue = position;

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[ERROR] {error}");
            }
            else
            {
                Console.WriteLine($"{output} Stepper {SerialNumber}  Position - {position}");
            }
        }



        public void SetCurrentPositionZero()
        {
            CurrentValue = 0;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = TicCmdPathName,
                    Arguments = $"--serial {SerialNumber} --halt-and-set-position 0",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            this.CurrentValue = 0;

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[ERROR] {error}");
            }
            else
            {
                Console.WriteLine($"{output} Stepper {SerialNumber}  Position - 0");
            }
        }
    }  
       
}