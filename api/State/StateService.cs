using System.Collections.Concurrent;
using System.Text.Json;
using api.Models.ServerEvents;
using infrastructure.QueryModels;
using lib;
using service.Services;
namespace api.State;
using Fleck;
using Timer = System.Timers.Timer;
public class WebSocketMetaData(IWebSocketConnection connection)
{
    public IWebSocketConnection Connection { get; set; } = connection;
    public string? Username { get; set; }
}
public class StateService { 
    public StateService(QuizManagerService quizManagerService)
    {
        _quizManagerService = quizManagerService;
        _quizManagerService.QuestionAsked += async (room, question, answer, currentQuestionNumber, totalQuestions) => await WaitForAnswers(room, question, answer, currentQuestionNumber, totalQuestions);
        //In this code, an async lambda function is used as the event handler for QuestionAsked. This lambda function matches the event signature and calls WaitForAnswers inside it.
        _quizManagerService.QuizStarted += GetNumberOfConnectionsInRoom;
        _quizManagerService.ScoreCalculated += SendScore;

    }
    
    private readonly QuizManagerService _quizManagerService;
    public ConcurrentDictionary<Guid, WebSocketMetaData> Connections { get; } = new();
    private ConcurrentDictionary<int, HashSet<Guid>> Rooms { get; } = new();
    public ConcurrentDictionary<int, Timer> SetupTimers { get; } = new ConcurrentDictionary<int, Timer>();
    private readonly ConcurrentDictionary<int, Dictionary<string, Dictionary<Question, Answer>>> _userAnswersPerRoom = new();
    private readonly ConcurrentDictionary<int, Question> _currentQuestionsPerRoom = new();
    


    private void GetNumberOfConnectionsInRoom(int room)
    {
        if (Rooms.TryGetValue(room, out var connections))
        {
            Console.WriteLine($"Number of connections in room: {room} is {connections.Count}");
        }
    }
    
    
    public bool AddConnection(IWebSocketConnection socket)
    {
        return Connections.TryAdd(socket.ConnectionInfo.Id, 
            new WebSocketMetaData(socket));
    }
   
    public bool AddToRoom(IWebSocketConnection socket, int room, string username)
    {
        // Set the username for the connection
        Connections[socket.ConnectionInfo.Id].Username = username;

        // Check if the username already exists in the room
        if (Rooms.TryGetValue(room, out var roomConnections))
        {
            if (roomConnections.Any(guid => Connections[guid].Username == username))
            {
                // Username already exists in the room, return false
                return false;
            }
        }

        // If the username does not exist in the room, add the connection to the room
        return AddConnectionToRoom(socket.ConnectionInfo.Id, room);
    }

    public bool CreateRoom(IWebSocketConnection socket, int room, string username)
    {
        if(!Rooms.ContainsKey(room))
            Rooms.TryAdd(room, new HashSet<Guid>());
        
        Console.WriteLine( Rooms.ContainsKey(room) ? "Room Created" : "Room not created");
        AddToRoom(socket, room, username);
        return AddConnectionToRoom(socket.ConnectionInfo.Id, room);
    }
    private bool AddConnectionToRoom(Guid connectionId, int room)
    {
        bool result = Rooms[room].Add(connectionId);

        // Call the new method to send the list of usernames to the room.
        SendUserListToRoom(room);

        return result;
    }
    
    private void SendUserListToRoom(int room)
    {
        // Get all usernames in the room.
        List<string?> usernamesInRoom = Rooms[room]
            .Select(guid => Connections[guid].Username)
            .Where(username => username != null)
            .ToList();

        // Remove the "Host" and 'username' username from the list.
        usernamesInRoom.Remove("Host");
        usernamesInRoom.Remove("username");
        Console.WriteLine("Usernames in room: " + string.Join(", ", usernamesInRoom));
        // Send a server event with the usernames.
        var serverEvent = new ServerMessage.ServerUserJoinedRoomEventDto
        {
            eventType = "ServerTellsUserJoinedRoom",
            Usernames = usernamesInRoom
        };
        SendServerResponse(room, serverEvent);
    }
    public bool KickUserFromRoom(int roomId, string username)
    {
        // Check if the room exists.
        if (Rooms.TryGetValue(roomId, out var roomConnections))
        {
            // Find the connection with the specified username.
            var userConnection = roomConnections.FirstOrDefault(guid => Connections[guid].Username == username);

            if (userConnection != default)
            {
                // Remove the user from the room.
                roomConnections.Remove(userConnection);
                // Create a ServerUserLeftRoomEventDto object.
                var serverEvent = new ServerMessage.ServerUserLeftRoomEventDto
                {
                    eventType = "ServerUserLeftRoom",
                    Username = username,
                    RoomId = roomId
                };
                // Send the server event.
                SendServerResponse(roomId, serverEvent);

                return true;
            }
        }

        // The room does not exist or the user was not found in the room.
        return false;
    }
    
    
    public bool KickAllUsersFromRoom(int room)
    {
        if (!Rooms.TryGetValue(room, out var guids))
        {
            return false;
        }
        foreach (var guid in guids)
        {
            if (Connections.TryGetValue(guid, out var ws))
                ws.Connection.Send("You have been kicked from the room");
        }
        guids.Clear();
        return true;
    }

    private void BroadcastToRoom(int room, string message, IWebSocketConnection? dontSentToThis = null)
    {
        if (Rooms.TryGetValue(room, out var guids))
        {
            foreach (var guid in guids)
            {
                if (Connections.TryGetValue(guid, out var ws) && ws.Connection != dontSentToThis)
                {
                    ws.Connection.Send(message);
                }
            }
        }
    }
    
    public void AddAnswer(String Username, Int32 room, Question question, Answer answer)
    {
        if (Username == null)
        {
            throw new ArgumentNullException(nameof(Username), "Username cannot be null.");
        }

        if (!_userAnswersPerRoom.TryGetValue(room, out var roomAnswers))
        {
            roomAnswers = new Dictionary<string, Dictionary<Question, Answer>>();
            _userAnswersPerRoom[room] = roomAnswers;
        }

        if (!roomAnswers.TryGetValue(Username, out var userAnswers))
        {
            userAnswers = new Dictionary<Question, Answer>();
            roomAnswers[Username] = userAnswers;
        }
        userAnswers[question] = answer;
    }

    private async Task WaitForAnswers(int room, Question question, List<Answer> answers, int currentQuestionNumber, int totalQuestions)
    {
        // Update the current question for the room
        SetCurrentQuestion(room, question, answers);
        Console.WriteLine($"Waiting for answers for question {currentQuestionNumber} of {totalQuestions}");
        // Create a new ServerCurrentQuestionInfo response
        var questionInfoResponse = new ServerMessage.ServerCurrentQuestionInfo
        {
            eventType = "ServerCurrentQuestionInfo",
            currentQuestionIndex = currentQuestionNumber,
            totalQuestions = totalQuestions
        };

        // Send the response to the room
        SendServerResponse(room, questionInfoResponse);
        // Update the current question for the room
        SetCurrentQuestion(room, question, answers);
        var timer = new Timer(QuizManagerService.DelayTimeMilliseconds);
        var tcs = new TaskCompletionSource<bool>();
        
        timer.Elapsed += (sender, e) => tcs.TrySetResult(true);
        timer.Start();
        // Send the initial ServerTimeRemaining response
        var initialResponse = new ServerMessage.ServerTimeRemaining
        {
            eventType = "ServerTimeRemaining",
            timeRemaining = QuizManagerService.DelayTimeMilliseconds
        };
        SendServerResponse(room, initialResponse);
        
        // Check if the room key exists in the dictionary, if not add it
        if (!_userAnswersPerRoom.TryGetValue(room, out var roomAnswers))
        {
            roomAnswers = new Dictionary<string, Dictionary<Question, Answer>>();
            _userAnswersPerRoom[room] = roomAnswers;
        }

        // This loop checks every second if all users have answered
        while (roomAnswers.Count != Rooms[room].Count - 1) // Check if all users have answered
        {
            if (await Task.WhenAny(Task.Delay(1000), tcs.Task) == tcs.Task)
            {
                break; // The timer has run out
            }
        }

        timer.Stop();

        // Print out the number of people who answered the question
        var response = new ServerMessage.ServerTellsHowManyPeopleAnswered
        {
            eventType = "ServerTellsHowManyPeopleAnswered",
            peopleAnswered = roomAnswers.Count
        };

        // Send the response to the room
        SendServerResponse(room, response);
        Console.WriteLine($"{roomAnswers.Count} people answered the question.");
    }
    
    public void StartQuiz(int quizRoomId, string quizId)
    {
        _ = _quizManagerService.RunQuiz(quizRoomId, quizId, GetUserInput, _userAnswersPerRoom);
    }
    private async Task<Answer> GetUserInput(Question question, List<Answer> answers)
    {
        // This is just a placeholder implementation.
        // You need to replace this with your actual code to get the user's input.
        await Task.Delay(1000); // Simulate a delay

        // Return the first answer as the user's input.
        // In your actual code, you should get the user's input and find the corresponding answer.
        return answers[0];
    }

    private void SetCurrentQuestion(int roomId, Question question, List<Answer> answers)
    {
        if (_currentQuestionsPerRoom.ContainsKey(roomId))
        {
            _currentQuestionsPerRoom[roomId] = question;
        }
        else
        {
            _currentQuestionsPerRoom.TryAdd(roomId, question);
        }
        question.QuizId = "";
        // Create a new ServerSetCurrentQuestion response
        var response = new ServerMessage.ServerSetCurrentQuestion
        {
            eventType = "ServerSetCurrentQuestion",
            question = question,
            answers = answers // Assuming the Question class has an Answers property
        };

        // Send the response to the room
        SendServerResponse(roomId, response);
    }

    public Question GetCurrentQuestion(int roomId)
    {
        if (_currentQuestionsPerRoom.TryGetValue(roomId, out var currentQuestion))
        {
            return currentQuestion;
        }
        else
        {
            throw new Exception("No current question for room " + roomId);
        }
    }
    private void SendScore(int roomId, Dictionary<string, int> scores)
    {
        var response = new ServerMessage.ServerShowScore
        {
            eventType = "ServerShowScore",
            scores = scores
        };

        SendServerResponse(roomId, response);
    }
    private void SendServerResponse<T>(int room, T response) where T : BaseDto
    {
        // Can take any BaseDto object and send it to the room
        // Serialize the response to a JSON string
        var message = JsonSerializer.Serialize(response);

        // Broadcast the message to the room
        BroadcastToRoom(room, message);
    }
}