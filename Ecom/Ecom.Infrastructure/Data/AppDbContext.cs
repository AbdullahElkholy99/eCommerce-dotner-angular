using Ecom.Core.Entity.Account;
using Ecom.Core.Entity.Order;
using Ecom.Core.Entity.Product;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Ecom.Infrastructure.Data;

//to create migration :  Add-Migration init -o "Data/Migrations"
public class AppDbContext : IdentityDbContext<User>
{
    // Constructor to pass options to the base DbContext class 
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    }

    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<Photo> Photos { get; set; }
    public virtual DbSet<Address> Addresses { get; set; }
    public virtual DbSet<Orders> Orders { get; set; }
    public virtual DbSet<OrderItem> OrderItems { get; set; }
    //public virtual DbSet<Rating> Ratings { get; set; }
    public virtual DbSet<DeliveryMethod> DeliveryMethods { get; set; }

}
