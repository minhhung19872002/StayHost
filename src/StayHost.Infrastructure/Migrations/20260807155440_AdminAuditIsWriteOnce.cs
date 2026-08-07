using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <summary>
    /// docs/08 §1.2 — "Nhật ký không ai xoá được, kể cả Quản trị tối cao", and
    /// §13 scenario 10 tests exactly that.
    ///
    /// The application already refuses to modify these rows, but "kể cả Quản trị
    /// tối cao" has to mean more than an if-statement somebody with a database
    /// connection can walk around. The rule belongs where the rows are.
    /// </summary>
    public partial class AdminAuditIsWriteOnce : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                create or replace function stayhost_audit_is_write_once()
                returns trigger as $$
                begin
                    raise exception
                        'Nhật ký quản trị chỉ được ghi thêm, không sửa và không xoá (docs/08 §1.2).';
                end;
                $$ language plpgsql;

                drop trigger if exists admin_audit_write_once on admin_audit;

                create trigger admin_audit_write_once
                before update or delete or truncate on admin_audit
                for each statement execute function stayhost_audit_is_write_once();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                drop trigger if exists admin_audit_write_once on admin_audit;
                drop function if exists stayhost_audit_is_write_once();
                """);
        }
    }
}
