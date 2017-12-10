using Service;

namespace OwinEngine
{
    public static class OwinHttpWorkerManage
    {
        public static void OwinHttpProcess(OwinSocket owinSocket)
        {
            new OwinHttpWorker(null).Start(owinSocket);
        }

        public static void Start(OwinSocket owinSocket, byte[] array)
        {
            new OwinHttpWorker(array).Start(owinSocket);
        }
    }
   
}
