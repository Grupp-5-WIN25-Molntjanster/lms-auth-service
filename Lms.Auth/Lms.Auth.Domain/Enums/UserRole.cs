namespace Lms.Auth.Domain.Enums;

/// <summary>
/// Defines the available user roles in the Learning Management System.
/// 
/// Role Hierarchy:
///   Admin > Instructor > Student
/// 
/// Usage:
/// - Student: Default role for new registrations. Can enroll in courses and view content.
/// - Instructor: Can create and manage courses, view student progress, and upload content.
/// - Admin: Full system access including user management and configuration.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Default role for learners. Can view courses, enroll, and track progress.
    /// </summary>
    Student = 0,

    /// <summary>
    /// Course creator role. Can manage course content, view enrollment lists, and grade students.
    /// </summary>
    Instructor = 1,

    /// <summary>
    /// System administrator role. Has unrestricted access to all system features.
    /// </summary>
    Admin = 2
}