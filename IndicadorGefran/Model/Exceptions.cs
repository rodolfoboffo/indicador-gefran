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
        private readonly String details;

        public NoResponseFromIndicatorException()
        {
        }

        // details: contexto para diagnostico (tempo aguardado, bytes ja recebidos), anexado a Message.
        public NoResponseFromIndicatorException(String details)
        {
            this.details = details;
        }

        public override String Message
        {
            get
            {
                String message = "Não houve resposta do indicador.";
                return this.details == null ? message : message + " " + this.details;
            }
        }
    }

    public class InvalidResponseFromIndicatorException : Exception
    {
        private readonly String details;

        public InvalidResponseFromIndicatorException()
        {
        }

        // details: contexto para diagnostico (esperado x recebido), anexado a Message.
        public InvalidResponseFromIndicatorException(String details)
        {
            this.details = details;
        }

        public override String Message
        {
            get
            {
                String message = "Indicador respondeu de forma inválida.";
                return this.details == null ? message : message + " " + this.details;
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
