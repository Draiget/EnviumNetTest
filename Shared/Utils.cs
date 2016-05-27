using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace Shared
{
    public static class Utils
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);
        
        public static Random rand = new Random(DateTime.Now.Millisecond);

        public static int RandomInt(int min, int max) {
            return rand.Next(min, max);
        }

        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T> {
            if (value.CompareTo(max) > 0) {
                return max;
            }

            if (value.CompareTo(min) < 0) {
                return min;
            }

            return value;
        }

        public static T Min<T>(T a, T b) where T : IComparable<T> {
            if( a.CompareTo(b) < 0 ) {
                return a;
            }

            return b;
        }

        public static T Max<T>(T a, T b) where T : IComparable<T> {
            if( a.CompareTo(b) > 0 ) {
                return a;
            }

            return b;
        }

        public static bool CompareAddr(this EndPoint a, EndPoint b, bool onlyBase) {
            var aep = a as IPEndPoint;
            var bep = b as IPEndPoint;

            if( aep == null || bep == null) {
                return false;
            }

            if ( !onlyBase && aep.Port != bep.Port) {
                return false;
            }

            var aBt = aep.Address.GetAddressBytes();
            var bBt = bep.Address.GetAddressBytes();
            if( aBt[ 0 ] == bBt[ 0 ] && aBt[ 1 ] == bBt[ 1 ] && aBt[ 2 ] == bBt[ 2 ] && aBt[ 3 ] == bBt[ 3 ] ) {
                return true;
            }

            return false;
        }

        public static string ToString(this EndPoint a, bool onlyBase = false) {
            var ipe = a as IPEndPoint;
            if ( ipe == null ) {
                return a.AddressFamily.ToString();
            }

            return string.Format("{0}{1}", ipe.Address, onlyBase ? string.Empty : ":" + ipe.Port);
        }

        private static long _performanceFrequency = -1;
        private static long _clockStart;

        public static void InitTime() {
            if( _performanceFrequency == -1 ) {
                QueryPerformanceFrequency(out _performanceFrequency);
                QueryPerformanceCounter(out _clockStart);
            }
        }

        public static double SysTime() {
            InitTime();

            long currentTime;
            QueryPerformanceCounter( out currentTime );

            var rawSeconds = (double)( currentTime - _clockStart ) / (double)_performanceFrequency;
            return rawSeconds;
        }
    }
}
