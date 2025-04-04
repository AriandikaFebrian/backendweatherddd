﻿// -----------------------------------------------------------------------------------
// Copyright DAD RnD 2024. All rights reserved.
// United Tractors DAD Mobile Web Help Desk (helpdesk.mobweb@unitedtractors.com)
// -----------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCa.Application.Common.Interfaces;
using NetCa.Domain.Constants;
using Quartz;

namespace NetCa.Infrastructure.Jobs;

/// <summary>
/// ProducerDbJob
/// </summary>
public class ProducerDbJob : BaseJob<ProducerDbJob>
{
    private readonly IProducerService _producerService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProducerDbJob"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="producerService"></param>
    /// <param name="logger"></param>
    public ProducerDbJob(
        IServiceScopeFactory serviceScopeFactory,
        IProducerService producerService,
        ILogger<ProducerDbJob> logger)
        : base(logger, serviceScopeFactory)
    {
        _producerService = producerService;
    }

    /// <summary>
    /// Execute
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task Execute(IJobExecutionContext context)
    {
        try
        {
            Logger.LogDebug("Process produce message from database");

            using var scope = ServiceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var entity = await db.MessageBroker
                .Where(x => !x.IsSend)
                .OrderBy(x => x.StoredDate)
                .Take(ProcessConstants.DefaultTotalMaxProcess)
                .AsNoTracking()
                .ToListAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var item in entity)
            {
                await _producerService.SendAsync(item.Topic, item.Message).ConfigureAwait(false);
            }

            db.MessageBroker.RemoveRange(entity);

            await db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            Logger.LogDebug("Produce message from database success");
        }
        catch (Exception e)
        {
            Logger.LogError("Error when running worker produce message from database: {Message}", e.Message);
        }

        Logger.LogDebug("Produce message from database done");
    }
}
