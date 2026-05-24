using LoggingService.Controllers;
using LoggingService.Data;
using LoggingService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LoggingService.Tests
{

    public class LogsControllerTests
    {

        private LoggingDbContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<LoggingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new LoggingDbContext(options);
            databaseContext.Database.EnsureCreated();
            return databaseContext;
        }

        [Fact]
        public async Task CreateLog_ShouldAddLogToDatabase()
        {

            var context = GetDatabaseContext();
            var controller = new LogsController(context);
            var logDto = new LogEntryDto
            {
                ServiceName = "TestService",
                LogLevel = "Information",
                Message = "Test message",
                Timestamp = DateTime.UtcNow
            };


            var result = await controller.CreateLog(logDto);


            Assert.IsType<OkResult>(result);
            Assert.Equal(1, await context.Logs.CountAsync());
            var savedLog = await context.Logs.FirstAsync();
            Assert.Equal("TestService", savedLog.ServiceName);
            Assert.Equal("Information", savedLog.LogLevel);
        }

        [Fact]
        public async Task GetLogs_ShouldReturnFilteredLogs()
        {

            var context = GetDatabaseContext();


            context.Logs.AddRange(new List<LogEntry>
            {
                new LogEntry { ServiceName = "AppA", LogLevel = "Error", Message = "Error 1", Timestamp = DateTime.UtcNow.AddMinutes(-10) },
                new LogEntry { ServiceName = "AppB", LogLevel = "Info", Message = "Info 1", Timestamp = DateTime.UtcNow.AddMinutes(-5) },
                new LogEntry { ServiceName = "AppA", LogLevel = "Info", Message = "Info 2", Timestamp = DateTime.UtcNow }
            });
            await context.SaveChangesAsync();

            var controller = new LogsController(context);


            var result = await controller.GetLogs(serviceName: "AppA", level: null, from: null, to: null);


            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var logs = Assert.IsAssignableFrom<IEnumerable<LogEntryDto>>(okResult.Value);


            Assert.Equal(2, logs.Count());
            Assert.All(logs, l => Assert.Equal("AppA", l.ServiceName));
        }
    }
}
