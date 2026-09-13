namespace Panaderia.Services.Implementations;

public static class CalendarioCaja
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);
    public static DateOnly Hoy => DateOnly.FromDateTime(Local(DateTime.UtcNow));
    public static DateOnly Lunes(DateOnly fecha) => fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7));
    public static DateTime InicioUtc(DateOnly fecha) => TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(TimeOnly.MinValue), Zona);
    public static DateTime DesdeLocal(DateTime fecha) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified), Zona);
}
