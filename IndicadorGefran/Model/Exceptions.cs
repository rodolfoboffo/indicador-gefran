using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndicadorGefran.Model.Exceptions
{
    public class SerialPortInvalidException : Exception
    {
        public override String Message
        {
            get
            {
                return "Porta Serial inválida.";
            }
        }
    }

    public class NoResponseFromIndicatorException : Exception
    {
        public override String Message
        {
            get
            {
                return "Não houve resposta do indicador.";
            }
        }
    }

    public class InvalidResponseFromIndicatorException : Exception
    {
        public override String Message
        {
            get
            {
                return "Indicador respondeu de forma inválida.";
            }
        }
    }

    public class IndicatorNotReadyException : Exception
    {
        public override String Message
        {
            get
            {
                return "O indicador não está pronto.";
            }
        }
    }
}
