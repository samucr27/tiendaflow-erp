namespace TiendaFlow.Domain;

public class InvalidAttendanceException : Exception
{
    public InvalidAttendanceException(string message) : base(message) { }
}

public class AttendanceRecordNotFoundException : Exception
{
    public AttendanceRecordNotFoundException(int id)
        : base($"Attendance record {id} was not found.") { }
}

public class InvalidShiftException : Exception
{
    public InvalidShiftException(string message) : base(message) { }
}

public class ShiftTemplateNotFoundException : Exception
{
    public ShiftTemplateNotFoundException(int id)
        : base($"Shift template {id} was not found.") { }
}

public class ShiftAssignmentNotFoundException : Exception
{
    public ShiftAssignmentNotFoundException(int id)
        : base($"Shift assignment {id} was not found.") { }
}
