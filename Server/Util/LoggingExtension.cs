using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Server.Util
{
    public static class Out
    {
        private static ConsoleColor _oldConsoleColor;

        private static string CurrentTimestamp {
            get { return string.Format("{0:00}:{1:00}:{2:00}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second); }
        }

        [StringFormatMethodAttribute("format")]
        public static void Msg(string format, params object[] args) {
            lock( Console.Out ) {
                Console.Write(CurrentTimestamp + " | ");
                Console.WriteLine(format, args);
            }
        }

        public static void Msg(params object[] args) {
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.Write(CurrentTimestamp + " | ");
                foreach (var arg in args) {
                    if (arg is ConsoleColor) {
                        Console.ForegroundColor = (ConsoleColor) arg;
                        continue;
                    }

                    Console.Write(arg);
                }
                Console.Write("\n");
                Console.ForegroundColor = _oldConsoleColor;
            }
        }

        [StringFormatMethodAttribute("format")]
        public static void MsgC(ConsoleColor color, string format, params object[] args) {
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(CurrentTimestamp + " | ");
                Console.WriteLine(format, args);
                Console.ForegroundColor = _oldConsoleColor;
            }
        }

        [StringFormatMethodAttribute("format")]
        public static void Error(string format, params object[] args) {
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write(CurrentTimestamp + " | [ERROR] ");
                Console.WriteLine(format, args);
                Console.ForegroundColor = _oldConsoleColor;
            }
        }

        [StringFormatMethodAttribute("format")]
        public static void Warning(string format, params object[] args) {
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(CurrentTimestamp + " | [WARNING] ");
                Console.WriteLine(format, args);
                Console.ForegroundColor = _oldConsoleColor;
            }
        }

        [StringFormatMethodAttribute("format")]
        public static void Exception(Exception err, string format, params object[] args) {
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("{0} | [EXCEPTION] {0} ({1}): {2}\n{3}", string.Format(format, args), CurrentTimestamp, err.GetType(), err.Message, err.StackTrace);
                Console.ForegroundColor = _oldConsoleColor;
            }
        }

        [StringFormatMethodAttribute("format")]
        public static void Debug(string format, params object[] args) {
            #if DEBUG
            lock( Console.Out ) {
                _oldConsoleColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("{0} | [DEBUG] {1}", CurrentTimestamp, string.Format(format, args));
                Console.ForegroundColor = _oldConsoleColor;
            }
            #endif
        }
    }
}
