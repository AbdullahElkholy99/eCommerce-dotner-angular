
namespace Ecom.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IImageManagementService _imageManagementService;
    private readonly IConnectionMultiplexer redis;
    private readonly UserManager<User> userManager;
    private readonly IEmailService emailService;
    private readonly SignInManager<User> signInManager;
    private readonly IGenerateToken token;
    public ICategoryRepository CategoryRepository { get; }

    public IPhotoRepository PhotoRepository { get; }

    public IProductRepository ProductRepository { get; }

    public ICustomerBasketRepository CustomerBasketRepository { get; }

    public IAuth Auth { get; }

    public UnitOfWork(AppDbContext context, IMapper mapper, IImageManagementService imageManagementService,

        IConnectionMultiplexer redis, UserManager<User> userManager, IEmailService emailService, SignInManager<User> signInManager, IGenerateToken token)
    {
        _context = context;
        _mapper = mapper;
        _imageManagementService = imageManagementService;
        this.redis = redis;
        this.userManager = userManager;
        this.emailService = emailService;
        this.signInManager = signInManager;
        this.token = token;
        CategoryRepository = new CategoryRepository(_context);
        PhotoRepository = new PhotoRepository(_context);
        ProductRepository = new ProductRepository(_context, _mapper, _imageManagementService);
        CustomerBasketRepository = new CustomerBasketRepository(redis);
        Auth = new AuthRepository(userManager, emailService, signInManager,token, context);
    }
}