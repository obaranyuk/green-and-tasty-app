using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.Dishes;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers;

public class GetAllSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockGuestSessionService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GetAllSessionsHandler _handler;

    public GetAllSessionsHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new GetAllSessionsHandler(_mockGuestSessionService.Object);
    }

    [Fact]
    public async Task HandleRequest_MissingGroupReservationId_Returns400()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("groupReservationId", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingAuthorizer_Returns401()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "group-1" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = null
            }
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(401, response.StatusCode);
        Assert.Contains("Unauthorized", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingSubClaim_Returns401()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "group-1" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>()
                }
            }
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(401, response.StatusCode);
        Assert.Contains("Unauthorized", response.Body);
    }

    [Fact]
    public async Task HandleRequest_GroupReservationNotFound_Returns404()
    {
        var request = CreateAuthorizedRequest("group-1", "user-1");

        _mockGuestSessionService
            .Setup(s => s.GetAllSessionsAsync("user-1", "group-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Group reservation was not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_Returns404()
    {
        var request = CreateAuthorizedRequest("group-1", "user-1");

        _mockGuestSessionService
            .Setup(s => s.GetAllSessionsAsync("user-1", "group-1"))
            .ThrowsAsync(new ReservationNotFoundException("Parent reservation not found."));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Reservation not found", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ForbiddenUser_Returns403()
    {
        var request = CreateAuthorizedRequest("group-1", "user-1");

        _mockGuestSessionService
            .Setup(s => s.GetAllSessionsAsync("user-1", "group-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("Only the reservation owner can perform this action", response.Body);
    }

    [Fact]
    public async Task HandleRequest_ValidRequest_Returns200WithCamelCaseJson()
    {
        var request = CreateAuthorizedRequest("group-1", "user-1");

        var dish = new DishResponse
        {
            Id = "dish1",
            Name = "Caesar Salad",
            Price = 180,
            Calories = "200",
            Carbohydrates = "10",
            Description = "Salad",
            DishType = "Appetizer",
            Fats = "5",
            ImageUrl = null,
            Proteins = "8",
            State = "Available",
            Vitamins = "A",
            Weight = "250g"
        };

        var participants = new List<Participant>
        {
            new(
                "session1",
                ParticipantType.Guest,
                "Andrii",
                new List<OrderItemResponse>
                {
                    new() { Dish = dish, Quantity = 2 }
                },
                360,
                true)
        };

        var serviceResponse = new GetAllSessionsResponse(participants, 360);

        _mockGuestSessionService
            .Setup(s => s.GetAllSessionsAsync("user-1", "group-1"))
            .ReturnsAsync(serviceResponse);

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);

        // Verify JSON camelCase formatting as requested in spec
        using var doc = JsonDocument.Parse(response.Body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("participants", out var participantsElem));
        Assert.True(root.TryGetProperty("total", out var totalElem));
        Assert.Equal(360, totalElem.GetDecimal());

        var firstParticipant = participantsElem[0];
        Assert.Equal("session1", firstParticipant.GetProperty("id").GetString());
        Assert.Equal("Guest", firstParticipant.GetProperty("type").GetString());
        Assert.Equal("Andrii", firstParticipant.GetProperty("name").GetString());
        Assert.True(firstParticipant.GetProperty("isSubmitted").GetBoolean());
        Assert.Equal(360, firstParticipant.GetProperty("total").GetDecimal());

        var firstItem = firstParticipant.GetProperty("items")[0];
        Assert.Equal(2, firstItem.GetProperty("quantity").GetInt32());
        var dishElem = firstItem.GetProperty("dish");
        Assert.Equal("dish1", dishElem.GetProperty("id").GetString());
        Assert.Equal("Caesar Salad", dishElem.GetProperty("name").GetString());
        Assert.Equal(180, dishElem.GetProperty("price").GetDecimal());
    }

    private static APIGatewayProxyRequest CreateAuthorizedRequest(string groupReservationId, string userId)
    {
        return new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", groupReservationId }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string>
                    {
                        { "sub", userId }
                    }
                }
            }
        };
    }
}
