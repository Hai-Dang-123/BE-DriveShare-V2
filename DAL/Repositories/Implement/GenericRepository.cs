using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {

        protected readonly DriverShareAppContext _context; // Change to protected
        private readonly DbSet<T> _dbSet;

        public GenericRepository(DriverShareAppContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }


        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public T Add(T entity)
        {
            _dbSet.Add(entity);
            return entity;
        }

        public bool Delete(T entity)
        {
            _dbSet.Remove(entity);
            return true;
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                Delete(entity);
                await _context.SaveChangesAsync();
            }
        }

        public IQueryable<T> FindAll(Expression<Func<T, bool>> expression)
        {
            return _dbSet.Where(expression).AsQueryable();
        }

        public IEnumerable<T> FindAllAsync(Expression<Func<T, bool>> expression)
        {
            return _dbSet.Where(expression).AsEnumerable();
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.AnyAsync(expression);
        }

        public IQueryable<T> GetAll()
        {
            return _dbSet.AsQueryable();
        }

        public async Task<List<T>> GetAllByListAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.Where(expression).ToListAsync();
        }

        public async Task<List<T>> GetAllByListAsync(Expression<Func<T, bool>> expression, object include)
        {
            return await _dbSet.Where(expression).ToListAsync();
        }

        public T GetById(string id)
        {
            throw new NotImplementedException();
        }
        public T GetByGuid(Guid id)
        {
            return _dbSet.Find(id);
        }

        public async Task<T> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty", nameof(id));
            }

            return await _dbSet.FindAsync(id);
        }

        public async Task<T> GetByIdsAsync(int id)
        {
            if (id == 0)
            {
                throw new ArgumentException("Id cannot be zero", nameof(id));
            }

            return await _dbSet.FindAsync(id);
        }

        public async Task<T> UpdateAsync(T entity)
        {
            _dbSet.Update(entity);

            return entity;
        }


        public void UpdateRange(List<T> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public void RemoveRange(List<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }


        public void AddRange(List<T> entity)
        { _dbSet.AddRange(entity); }

        // Đổi từ void -> Task và dùng AddRangeAsync
        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }
        public async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.FirstOrDefaultAsync(expression);
        }

        public async Task<T> GetByConditionAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.FirstOrDefaultAsync(expression);
        }

        public Task<List<T>> ToListAsync()
        {
            return _dbSet.ToListAsync();
        }

        /// <summary>
        /// Lấy tất cả các thực thể một cách bất đồng bộ với các tùy chọn lọc,
        /// sắp xếp, và gộp bảng (include).
        /// </summary>
        /// <param name="filter">Biểu thức lọc (Where clause). Ví dụ: u => u.Status == "Active"</param>
        /// <param name="orderBy">Hàm để sắp xếp. Ví dụ: q => q.OrderBy(u => u.Name)</param>
        /// <param name="includeProperties">Chuỗi tên các navigation property cần gộp, cách nhau bởi dấu phẩy. Ví dụ: "User,Trip"</param>
        /// <returns>Một danh sách (List) các thực thể.</returns>
        /// 
//        List<Driver> topOnlineDrivers = await _driverRepository.GetAllAsync(
//        filter: d => d.AppStatus == DriverAppStatus.ONLINE,
//        orderBy: q => q.OrderByDescending(d => d.AverageRating),
//        includeProperties: "Vehicle"
//);
        // (Giả sử Driver có navigation property tên là "Vehicle")
        public async Task<List<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string? includeProperties = null)
        {
            IQueryable<T> query = _dbSet;

            // 1. Áp dụng Filter (Where)
            if (filter != null)
            {
                query = query.Where(filter);
            }

            // 2. Áp dụng Include (Join)
            if (includeProperties != null)
            {
                // Tách chuỗi "User,Trip" thành ["User", "Trip"]
                foreach (var includeProperty in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty);
                }
            }

            // 3. Áp dụng Sắp xếp (Order By)
            if (orderBy != null)
            {
                query = orderBy(query);
            }

            // 4. Thực thi truy vấn và trả về kết quả
            return await query.ToListAsync();
        }

        // đây là get all async bình thường
        public async Task<List<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        public async Task<T?> FirstOrDefaultAsync(
    Expression<Func<T, bool>> filter,
    string includeProperties = "")
        {
            IQueryable<T> query = _dbSet;

            // Phân tích chuỗi "ShippingRoute,Vehicle.VehicleType"
            foreach (var includeProperty in includeProperties.Split(
                new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty); // Dùng Include(string)
            }

            return await query.FirstOrDefaultAsync(filter);
        }
    }
}
