using IdopontfoglaloWebalk.Context;
using IdopontfoglaloWebalk.Models;
using IdopontfoglaloWebalk.BackgroundTasks.Cron;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IdopontfoglaloWebalk.BackgroundTasks
{
    public class SendRemindersCronJob : CronJobService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SendRemindersCronJob> _logger;

        public SendRemindersCronJob(
            IScheduleConfig<SendRemindersCronJob> config,
            IServiceScopeFactory scopeFactory,
            ILogger<SendRemindersCronJob> logger)
            : base(config.CronExpression, config.TimeZoneInfo)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override async Task DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CronJob elindult: Előző napi emlékeztetők feldolgozása...");

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EfContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSenderService>();

            var tomorrow = DateTime.Today.AddDays(1);
            var tomorrowStart = tomorrow.Date;
            var tomorrowEnd = tomorrow.Date.AddDays(1).AddTicks(-1);

            var occasions = await context.Occasions
                .Include(o => o.User)
                .Include(o => o.ServiceCategory)
                    .ThenInclude(sc => sc.Service)
                        .ThenInclude(s => s.Owner)
                .Where(o => o.status == "reserved"
                            && !o.reminder_sent
                            && o.date >= tomorrowStart
                            && o.date <= tomorrowEnd
                            && !string.IsNullOrEmpty(o.user_id))
                .ToListAsync(cancellationToken);

            if (!occasions.Any())
            {
                _logger.LogInformation("Nincs holnapra esedékes kiküldendő emlékeztető.");
                return;
            }

            foreach (var occ in occasions)
            {
                var guest = occ.User;
                var service = occ.ServiceCategory?.Service;
                var owner = service?.Owner;

                var serviceName = service?.name ?? "Szolgáltatás";
                var serviceAddress = service?.address ?? "Cím nem elérhető";
                var categoryName = occ.ServiceCategory?.Name ?? "Általános";
                var timeStr = occ.date.ToString("yyyy. MM. dd. HH:mm");

                if (guest != null && !string.IsNullOrEmpty(guest.Email))
                {
                    var subject = $"Emlékeztető: Holnapi időpontod - {serviceName}";
                    var body = $@"
                        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                            <h2 style=""color: #0d6efd;"">Kedves {guest.UserName}!</h2>
                            <p>Szeretnénk emlékeztetni, hogy <strong>holnap időpontod van</strong> lefoglalva nálunk.</p>
                            <div style=""background-color: #f8f9fa; border-left: 4px solid #0d6efd; padding: 15px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Szolgáltatás:</strong> {serviceName}</p>
                                <p style=""margin: 5px 0;""><strong>Kategória:</strong> {categoryName}</p>
                                <p style=""margin: 5px 0;""><strong>Időpont:</strong> {timeStr}</p>
                                <p style=""margin: 5px 0;""><strong>Helyszín:</strong> {serviceAddress}</p>
                            </div>
                            <p>Kérjük, érkezz pontosan!</p>
                            <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                        </div>";

                    try
                    {
                        await emailSender.SendEmailAsync(guest.Email, subject, body);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Hiba a vendég email küldésekor ({guest.Email}): {ex.Message}");
                    }
                }

                if (owner != null && !string.IsNullOrEmpty(owner.Email))
                {
                    var ownerSubject = $"Vendég emlékeztető holnapra - {guest?.UserName ?? "Vendég"} ({timeStr})";
                    var ownerBody = $@"
                        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
                            <h2 style=""color: #198754;"">Kedves {owner.UserName}!</h2>
                            <p>Értesítünk, hogy holnap az alábbi vendéget várod:</p>
                            <div style=""background-color: #f8f9fa; border-left: 4px solid #198754; padding: 15px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Vendég:</strong> {guest?.UserName ?? "Ismeretlen"}</p>
                                <p style=""margin: 5px 0;""><strong>Telefon:</strong> {guest?.PhoneNumber ?? "Nincs megadva"}</p>
                                <p style=""margin: 5px 0;""><strong>Időpont:</strong> {timeStr}</p>
                                <p style=""margin: 5px 0;""><strong>Szolgáltatás típusa:</strong> {categoryName}</p>
                            </div>
                            <p>Üdvözlettel,<br />A SlotHub Csapata</p>
                        </div>";

                    try
                    {
                        await emailSender.SendEmailAsync(owner.Email, ownerSubject, ownerBody);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Hiba a szolgáltatói email küldésekor ({owner.Email}): {ex.Message}");
                    }
                }

                occ.reminder_sent = true;
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("CronJob befejeződött: {Count} db emlékeztető kiküldve.", occasions.Count);
        }
    }
}