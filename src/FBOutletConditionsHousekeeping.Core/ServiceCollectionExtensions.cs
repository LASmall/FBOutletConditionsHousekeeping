using Azure.Identity;
using CloudinaryDotNet;
using FBOutletConditionsHousekeeping.Core.Configuration;
using FBOutletConditionsHousekeeping.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace FBOutletConditionsHousekeeping.Core;

/// <summary>
/// Composition root for the cleanup feature: binds configuration sections,
/// registers startup validation, and wires the SharePoint/Cloudinary/Email
/// service implementations and the orchestrator. Kept in Core (rather than
/// the Functions project) so the whole feature can be registered with a
/// single call from any host.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string[] GraphDefaultScope = ["https://graph.microsoft.com/.default"];

    public static IServiceCollection AddCleanupServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CleanupOptions>()
            .Bind(configuration.GetSection(CleanupOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CleanupOptions>, CleanupOptionsValidator>();

        services.AddOptions<SharePointOptions>()
            .Bind(configuration.GetSection(SharePointOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton(serviceProvider =>
        {
            var sharePointOptions = serviceProvider.GetRequiredService<IOptions<SharePointOptions>>().Value;
            var credential = new ClientSecretCredential(
                sharePointOptions.TenantId, sharePointOptions.ClientId, sharePointOptions.ClientSecret);
            return new GraphServiceClient(credential, GraphDefaultScope);
        });

        services.AddSingleton(serviceProvider =>
        {
            var cloudinaryOptions = serviceProvider.GetRequiredService<IOptions<CloudinaryOptions>>().Value;
            var account = new Account(cloudinaryOptions.CloudName, cloudinaryOptions.ApiKey, cloudinaryOptions.ApiSecret);
            return new Cloudinary(account);
        });

        services.AddScoped<ISharePointReportLogService, GraphSharePointReportLogService>();
        services.AddScoped<ICloudinaryImageService, CloudinaryImageService>();
        services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
        services.AddScoped<IEmailNotificationService, MailKitEmailNotificationService>();
        services.AddScoped<ICleanupOrchestrator, CleanupOrchestrator>();

        return services;
    }
}
