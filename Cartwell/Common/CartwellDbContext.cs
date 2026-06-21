using Microsoft.EntityFrameworkCore;

namespace Cartwell.Common;

public class CartwellDbContext(DbContextOptions<CartwellDbContext> options)
	: DbContext(options)
{
}
