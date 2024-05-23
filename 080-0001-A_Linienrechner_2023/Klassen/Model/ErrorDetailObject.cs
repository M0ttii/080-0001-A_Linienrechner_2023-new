using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linienrechner.Klassen.Model
{
    /// <summary>Objekt für die Fehlermeldungen</summary>
    internal class ErrorDetailObject
    {

        public ErrorDetailObject()
        {
        }

        public ErrorDetailObject(string fehlerText, string detail)
        {
            FehlerText = fehlerText;
            Detail = detail;
        }

        public string FehlerText { get; set; }
        public string Detail { get; set; }
    }
}
