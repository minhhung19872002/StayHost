using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <summary>
    /// docs/09 §3.4 — repairs the working week on services that predate it.
    ///
    /// `WorkingDaysMask` was added by ServiceOptionsAndSchedule with
    /// `defaultValue: 0`, so every service already on sale came out of that
    /// migration working no day of the week. `ServiceRules.WorksOn` then said no
    /// to every date, which meant `CanBook` refused every request and the slot
    /// picker had nothing to offer — a listing that looked perfectly normal and
    /// could not be booked by anybody, with nothing in any log to say why.
    ///
    /// The reader now treats an out-of-range mask as the whole week, so nothing
    /// depends on this running; it is here so the stored value stops being a lie
    /// the moment it is read by anything that has not been taught the rule — the
    /// host's own editor, a report, a future query.
    /// </summary>
    public partial class RepairServiceWorkingWeek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                update service_offerings
                   set "WorkingDaysMask" = 127
                 where "WorkingDaysMask" <= 0 or "WorkingDaysMask" > 127;
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Putting "works no day of the week" back would only break the
            // listings again, so there is nothing to undo.
        }
    }
}
