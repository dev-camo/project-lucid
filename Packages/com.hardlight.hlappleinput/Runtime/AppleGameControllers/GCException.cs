// Modified from Apple unityplugins: preserve the shipped plural exception namespace and genuine Core InteropError parameter.
using System;
using Apple.Core.Runtime;

namespace Apple.GameControllers
{
    public class GCException : Exception
    {
        public int Code { get; private set; }
        public string LocalizedDescription { get; private set; }

        public GCException(InteropError error)
        {
            Code = error.Code;
            LocalizedDescription = error.LocalizedDescription;
        }
    }
}