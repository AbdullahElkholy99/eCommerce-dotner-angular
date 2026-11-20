using Ecom.Core.Entity.Order;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace Ecom.Infrastructure.Data.Configuration;

public class OrderItemConfiguratrion : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(m => m.Price).HasColumnType("decimal(18,2)");
    }
}
