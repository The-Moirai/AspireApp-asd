using Microsoft.EntityFrameworkCore;
using WebApplication.Data;
using ClassLibrary_Core.Drone;

namespace WebApplication.Service
{
    public class DroneService : IDroneService
    {
        private readonly ApplicationDbContext _context;

        public DroneService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Drone>> GetAllDronesAsync()
        {
            return await _context.Drones.ToListAsync();
        }

        public async Task<Drone> GetDroneByIdAsync(int id)
        {
            return await _context.Drones.FindAsync(id);
        }

        public async Task<Drone> CreateDroneAsync(Drone drone)
        {
            _context.Drones.Add(drone);
            await _context.SaveChangesAsync();
            return drone;
        }

        public async Task<Drone> UpdateDroneAsync(Drone drone)
        {
            _context.Entry(drone).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return drone;
        }

        public async Task DeleteDroneAsync(int id)
        {
            var drone = await _context.Drones.FindAsync(id);
            if (drone != null)
            {
                _context.Drones.Remove(drone);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Drone> UpdateDroneStatusAsync(int id, DroneStatus status)
        {
            var drone = await _context.Drones.FindAsync(id);
            if (drone != null)
            {
                drone.Status = status;
                await _context.SaveChangesAsync();
            }
            return drone;
        }

        public async Task<Drone> UpdateDronePositionAsync(int id, GPSPosition position)
        {
            var drone = await _context.Drones.FindAsync(id);
            if (drone != null)
            {
                drone.CurrentPosition = position;
                await _context.SaveChangesAsync();
            }
            return drone;
        }
    }
} 