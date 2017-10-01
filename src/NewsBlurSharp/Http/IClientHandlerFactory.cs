using System;
using System.Collections.Generic;
using System.Text;

namespace NewsBlurSharp.Http
{
    public interface IClientHandlerFactory
    {
        IClientHandler CreateHandler();
    }
}
