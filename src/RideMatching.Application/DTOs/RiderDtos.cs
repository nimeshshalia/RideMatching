namespace RideMatching.Application.DTOs;

public record CreateRiderRequestDto(
    string Name,
    string Email,
    string Phone);

public record RiderDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    double Rating);
