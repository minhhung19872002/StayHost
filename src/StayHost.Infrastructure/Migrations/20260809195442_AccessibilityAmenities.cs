using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <summary>
    /// docs/01 TM-17 — the accessibility amenities as reference data. The seed adds
    /// them on a fresh database, but reference rows also have to reach a database
    /// seeded before they existed, so they are inserted here too. Guarded by the
    /// unique key, so running it where the seed already added them is a no-op.
    /// </summary>
    public partial class AccessibilityAmenities : Migration
    {
        private static readonly (string Key, string Label, string Icon)[] Items =
        [
            ("step-free", "Lối vào bằng phẳng", "▱"),
            ("elevator", "Thang máy", "▤"),
            ("wide-door", "Cửa rộng cho xe lăn", "◫"),
            ("grab-bars", "Tay vịn trong phòng tắm", "▬"),
            ("ground-floor", "Phòng tầng trệt", "▦")
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sort after everything already there, so the new group lands at the end
            // of the filter panel rather than inside an existing group.
            var order = 100;
            foreach (var (key, label, icon) in Items)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO amenities ("Key", "Label", "Icon", "Group", "IsFilterable", "SortOrder")
                    SELECT '{key}', '{label}', '{icon}', 'Tiếp cận', TRUE, {order}
                    WHERE NOT EXISTS (SELECT 1 FROM amenities WHERE "Key" = '{key}');
                    """);
                order++;
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, _, _) in Items)
                migrationBuilder.Sql($"DELETE FROM amenities WHERE \"Key\" = '{key}';");
        }
    }
}
