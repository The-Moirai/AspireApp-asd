using ClassLibrary_Core.Drone;

namespace WebApplication.Service
{
    public interface IDroneService
    {
        Task<IEnumerable<Drone>> GetAllDronesAsync();
        Task<Drone> GetDroneByIdAsync(int id);
        Task<Drone> CreateDroneAsync(Drone drone);
        Task<Drone> UpdateDroneAsync(Drone drone);
        Task DeleteDroneAsync(int id);
        Task<Drone> UpdateDroneStatusAsync(int id, DroneStatus status);
        Task<Drone> UpdateDronePositionAsync(int id, GPSPosition position);
    }
} 