namespace Owin.AspEngine
{
    using System;
    using System.Runtime.CompilerServices;

    internal class AspRequestBroker : MarshalByRefObject
    {
        private DelegateDomainUnload _domainUnlocad;
        private DelegateRead _read;
        private DelegateRequestEnd _requestEnd;
        private DelegateWrite _write;
        private DelegateWriteHeader _writeHeader;
        private DelegateWriteHttpStatus _writeStatus;

        public AspRequestBroker(DelegateRead read, DelegateWrite write, DelegateWriteHeader writeHeader, DelegateWriteHttpStatus writeStatus, DelegateRequestEnd reqEnd, DelegateDomainUnload domainUnload)
        {
            this._read = read;
            this._write = write;
            this._writeHeader = writeHeader;
            this._writeStatus = writeStatus;
            this._requestEnd = reqEnd;
            this._domainUnlocad = domainUnload;
        }

        public void DomainUnload()
        {
            this._domainUnlocad();
        }

        public override object InitializeLifetimeService() => 
            null;

        public int Read(int id, byte[] buffer, int offset, int size) => 
            this._read(id, buffer, offset, size);

        public void RequestEnd(int id, bool keep)
        {
            this._requestEnd(id, keep);
        }

        public void Write(int id, byte[] buffer, int offset, int size)
        {
            this._write(id, buffer, offset, size);
        }

        public void WriteHeader(int id, string name, string value)
        {
            try
            {
                this._writeHeader(id, name, value);
            }
            catch (Exception exception)
            {
                Console.WriteLine("**** writeHandler: {0}", exception);
                throw;
            }
        }

        public void WriteStatus(int id, int statusCode, string statusDescription)
        {
            try
            {
                this._writeStatus(id, statusCode, statusDescription);
            }
            catch
            {
                Console.WriteLine("*** write status");
                throw;
            }
        }

        public delegate void DelegateDomainUnload();

        public delegate int DelegateRead(int id, byte[] buffer, int offset, int size);

        public delegate void DelegateRequestEnd(int id, bool keep);

        public delegate void DelegateWrite(int id, byte[] buffer, int offset, int size);

        public delegate void DelegateWriteHeader(int id, string name, string value);

        public delegate void DelegateWriteHttpStatus(int id, int statusCode, string statusDescription);
    }
}

