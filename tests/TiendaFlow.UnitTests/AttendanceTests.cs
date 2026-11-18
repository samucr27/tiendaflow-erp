using System.Globalization;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Invariants of the attendance and shift aggregates. A day is either worked
/// or covered by a novelty, and a scheduled shift keeps its own times so a
/// template change never rewrites history.
/// </summary>
public class AttendanceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Day = new(2026, 11, 16);

    private static AttendanceRecord Record() => new(0, 3, 1, Day, Now);

    [Fact]
    public void AttendanceStartsWithoutMarksOrNovelty()
    {
        var record = Record();

        Assert.False(record.IsPresent);
        Assert.False(record.IsOpen);
        Assert.False(record.HasNovelty);
        Assert.Equal(0m, record.HoursWorked);
        Assert.Equal(Now, record.CreatedAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void AttendanceRequiresAnEmployeeAStoreAndADate(int employeeId, int storeId)
    {
        Assert.Throws<InvalidAttendanceException>(() =>
            new AttendanceRecord(0, employeeId, storeId, Day, Now));
    }

    [Fact]
    public void CheckInThenCheckOutComputesWorkedHours()
    {
        var record = Record();

        record.RegisterCheckIn(new DateTime(2026, 11, 16, 13, 0, 0, DateTimeKind.Utc));
        Assert.True(record.IsPresent);
        Assert.True(record.IsOpen);

        record.RegisterCheckOut(new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc));

        Assert.False(record.IsOpen);
        Assert.Equal(8m, record.HoursWorked);
    }

    [Fact]
    public void CheckInCannotBeRegisteredTwice()
    {
        var record = Record();
        record.RegisterCheckIn(Now);

        Assert.Throws<InvalidAttendanceException>(() => record.RegisterCheckIn(Now));
    }

    [Fact]
    public void CheckOutRequiresAnEarlierCheckInAndHappensOnce()
    {
        var record = Record();

        Assert.Throws<InvalidAttendanceException>(() => record.RegisterCheckOut(Now));

        record.RegisterCheckIn(Now);
        Assert.Throws<InvalidAttendanceException>(() => record.RegisterCheckOut(Now));
        Assert.Throws<InvalidAttendanceException>(() => record.RegisterCheckOut(Now.AddHours(-1)));

        record.RegisterCheckOut(Now.AddHours(8));
        Assert.Throws<InvalidAttendanceException>(() => record.RegisterCheckOut(Now.AddHours(9)));
    }

    [Theory]
    [InlineData(AttendanceNovelty.JustifiedAbsence)]
    [InlineData(AttendanceNovelty.UnjustifiedAbsence)]
    [InlineData(AttendanceNovelty.Leave)]
    [InlineData(AttendanceNovelty.Vacation)]
    public void NoveltiesAreStoredWithoutProducingWorkedHours(AttendanceNovelty novelty)
    {
        var record = Record();

        record.RegisterNovelty(novelty, "Motivo de prueba", Now);

        Assert.True(record.HasNovelty);
        Assert.Equal(novelty, record.Novelty);
        Assert.Equal("Motivo de prueba", record.NoveltyReason);
        Assert.Equal(0m, record.HoursWorked);
    }

    [Fact]
    public void AnUnjustifiedAbsenceNeedsNoReasonButTheOthersDo()
    {
        var unjustified = Record();
        unjustified.RegisterNovelty(AttendanceNovelty.UnjustifiedAbsence, null, Now);
        Assert.Null(unjustified.NoveltyReason);

        var justified = Record();
        Assert.Throws<InvalidAttendanceException>(() =>
            justified.RegisterNovelty(AttendanceNovelty.JustifiedAbsence, "  ", Now));

        Assert.Throws<InvalidAttendanceException>(() =>
            Record().RegisterNovelty(AttendanceNovelty.Leave, new string('x', 301), Now));
    }

    [Fact]
    public void AMarkedDayCannotBecomeANoveltyAndViceVersa()
    {
        var withCheckIn = Record();
        withCheckIn.RegisterCheckIn(Now);
        Assert.Throws<InvalidAttendanceException>(() =>
            withCheckIn.RegisterNovelty(AttendanceNovelty.Leave, "Motivo", Now));

        var withNovelty = Record();
        withNovelty.RegisterNovelty(AttendanceNovelty.Vacation, "Motivo", Now);
        Assert.Throws<InvalidAttendanceException>(() => withNovelty.RegisterCheckIn(Now));
        Assert.Throws<InvalidAttendanceException>(() =>
            withNovelty.RegisterNovelty(AttendanceNovelty.Leave, "Otro", Now));
    }

    [Fact]
    public void AnUnknownNoveltyIsRejected()
    {
        Assert.Throws<InvalidAttendanceException>(() =>
            Record().RegisterNovelty((AttendanceNovelty)99, "Motivo", Now));
    }

    private static ShiftTemplate Template() =>
        new(0, "Mañana", new TimeOnly(7, 0), new TimeOnly(15, 0), Now);

    [Fact]
    public void ATemplateTrimsItsNameAndComputesDuration()
    {
        var template = new ShiftTemplate(0, "  Mañana  ", new TimeOnly(7, 0), new TimeOnly(15, 0), Now);

        Assert.Equal("Mañana", template.Name);
        Assert.True(template.Active);
        Assert.Equal(8m, template.DurationHours);

        template.Deactivate();
        Assert.False(template.Active);
    }

    [Theory]
    [InlineData("", "07:00", "15:00")]
    [InlineData("Mañana", "15:00", "15:00")]
    [InlineData("Mañana", "16:00", "15:00")]
    public void ATemplateNeedsANameAndAnEndAfterTheStart(
        string name,
        string start,
        string end)
    {
        Assert.Throws<InvalidShiftException>(() => new ShiftTemplate(
            0,
            name,
            TimeOnly.ParseExact(start, "HH:mm", CultureInfo.InvariantCulture),
            TimeOnly.ParseExact(end, "HH:mm", CultureInfo.InvariantCulture),
            Now));
    }

    [Fact]
    public void ARecurringTemplateCannotExceedTheNameLimit()
    {
        Assert.Throws<InvalidShiftException>(() => new ShiftTemplate(
            0,
            new string('x', ShiftTemplate.MaxNameLength + 1),
            new TimeOnly(7, 0),
            new TimeOnly(15, 0),
            Now));
    }

    private static ShiftAssignment Assignment(
        TimeOnly start = default,
        TimeOnly end = default,
        DateOnly date = default) => new(
            0,
            4,
            1,
            7,
            date == default ? Day : date,
            start == default ? new TimeOnly(7, 0) : start,
            end == default ? new TimeOnly(15, 0) : end,
            Now);

    [Fact]
    public void AnAssignmentCopiesTheTemplateTimesAndComputesDuration()
    {
        var assignment = Assignment();

        Assert.Equal(7, assignment.ShiftTemplateId);
        Assert.Equal(8m, assignment.DurationHours);
        Assert.False(assignment.Overlaps(new TimeOnly(15, 0), new TimeOnly(23, 0)));
    }

    [Fact]
    public void OverlappingShiftsAreDetectedOnlyOnTheSameDate()
    {
        var first = Assignment();
        var overlapping = Assignment(new TimeOnly(14, 0), new TimeOnly(22, 0));
        var nextDay = Assignment(new TimeOnly(7, 0), new TimeOnly(15, 0), Day.AddDays(1));

        Assert.True(first.Overlaps(overlapping));
        Assert.True(overlapping.Overlaps(first));
        Assert.False(first.Overlaps(nextDay));
    }

    [Fact]
    public void APastAssignmentCannotBeCancelled()
    {
        var assignment = Assignment(date: Day.AddDays(-1));

        Assert.Throws<InvalidShiftException>(() => assignment.EnsureCancellable(Day));
        assignment.EnsureCancellable(Day.AddDays(-2));
    }

    [Fact]
    public void AnAssignmentRequiresAValidEmployeeStoreTemplateAndInterval()
    {
        Assert.Throws<InvalidShiftException>(() => new ShiftAssignment(
            0, 0, 1, 7, Day, new TimeOnly(7, 0), new TimeOnly(15, 0), Now));
        Assert.Throws<InvalidShiftException>(() => new ShiftAssignment(
            0, 4, 0, 7, Day, new TimeOnly(7, 0), new TimeOnly(15, 0), Now));
        Assert.Throws<InvalidShiftException>(() => new ShiftAssignment(
            0, 4, 1, 0, Day, new TimeOnly(7, 0), new TimeOnly(15, 0), Now));
    }
}
