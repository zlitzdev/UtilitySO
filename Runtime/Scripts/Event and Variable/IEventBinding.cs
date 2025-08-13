using System;

namespace Zlitz.General.UtilitySO
{
    public interface IEventBinding : IDisposable
    {
        void Release();

        void IDisposable.Dispose()
        {
            Release();
        }
    }
}
