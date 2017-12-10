
namespace Model
{
  
    public class UvPipeHandle : UvStreamHandle
    {
        public int PipePendingCount()
        {
            return LibUv.PipePendingCount(this);
        }

        public void PipeBind(string text)
        {
            LibUv.PipeBind(this, text);
        }

        /// <summary>
        /// Init
        /// </summary>
        /// <param name="loopHandle"></param>
        /// <param name="flag">若是 IPC 或命名管道,应该设置为 true</param>
        public void Init(LoopHandle loopHandle, bool flag = true)
        {
            Init(loopHandle.LibUv, loopHandle.LibUv.NamePipeHandleSize, loopHandle.LoopRunThreadId);
            LibUv.PipeInit(loopHandle, this, flag);
        }
    }
}
