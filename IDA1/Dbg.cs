using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDA1
{
    class Dbg
    {
        /// <summary>
        /// Envia un mensage a la consola de depuracion ('Salida' por defecto) incluyendo el nombre de archivo, linea y nombre de metodo. 
        /// </summary>
        /// <param name="message">Mensage a enviar</param>
        /// <param name="memberName"></param>
        /// <param name="sourceFilePath"></param>
        /// <param name="sourceLineNumber"></param>
        public static void Write(string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            System.Diagnostics.Debug.WriteLine("message: " + message);
            System.Diagnostics.Debug.WriteLine("member name: " + memberName);
            System.Diagnostics.Debug.WriteLine("source file path: " + sourceFilePath);
            System.Diagnostics.Debug.WriteLine("source line number: " + sourceLineNumber);
        }

    }
}
