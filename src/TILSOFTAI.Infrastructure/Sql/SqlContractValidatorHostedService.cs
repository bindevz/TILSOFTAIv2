using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Infrastructure.Sql;

public sealed class SqlContractValidatorHostedService : IHostedService
{
    private readonly SqlContractValidator _validator;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SqlContractValidatorHostedService> _logger;

    public SqlContractValidatorHostedService(
        SqlContractValidator validator,
        IHostEnvironment environment,
        ILogger<SqlContractValidatorHostedService> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Skipping SQL contract validation in Development environment.");
            return;
        }

        await _validator.ValidateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
