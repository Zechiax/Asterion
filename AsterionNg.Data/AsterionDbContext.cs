using Microsoft.EntityFrameworkCore;

namespace AsterionNg.Data;

public class AsterionDbContext(DbContextOptions<AsterionDbContext> options) : DbContext(options);