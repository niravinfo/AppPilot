using AppPilot.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppPilot.Services.Discovery;

public interface IServiceDiscoveryService
{
    Task<List<DiscoveredService>> DiscoverAsync(string rootDirectory);
}
