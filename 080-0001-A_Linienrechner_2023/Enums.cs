using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linienrechner
{
    internal class Enums
    {
        public enum CPU_Type
        {
            S7200 = 0,
            S7300 = 10,
            S7400 = 20
        }

        public enum ErrorCode
        {
            NoError = 0,
            WrongCPU_Type = 1,
            ConnectionError = 2,
            IPAdressNotAvailable,

            WrongVarFormat = 10,
            WrongNumberReceivedBytes = 11,

            SendData = 20,
            ReadData = 30,

            WriteData = 50,

            NoSocket = 100,
            UnknownError = 101
        }
    }
}
