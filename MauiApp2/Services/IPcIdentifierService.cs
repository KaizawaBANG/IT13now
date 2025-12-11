using System;
using System.Threading.Tasks;

namespace MauiApp2.Services
{
    public interface IPcIdentifierService
    {
        string GetPcIdentifier();
        Task<string> GetOrCreatePcIdentifierAsync();
    }
}


