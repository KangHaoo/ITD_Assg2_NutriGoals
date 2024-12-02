using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;

public class AuthManager : MonoBehaviour
{
    //Firebase variables
    [Header("Firebase")]
    public DependencyStatus dependencyStatus;
    public FirebaseAuth auth;
    public FirebaseUser User;
    public DatabaseReference dbReference;

    //Login variables
    [Header("Login")]
    public TMP_InputField emailLoginField;
    public TMP_InputField passwordLoginField;
    public TMP_Text warningLoginText;
    public TMP_Text confirmLoginText;

    //Register variables
    [Header("Register")]
    public TMP_InputField usernameRegisterField;
    public TMP_InputField emailRegisterField;
    public TMP_InputField passwordRegisterField;
    public TMP_InputField passwordRegisterVerifyField;
    public TMP_Text warningRegisterText;

    void Awake()
    {
        //Check that all of the necessary dependencies for Firebase are present on the system
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                //If they are available, Initialize Firebase
                InitializeFirebase();
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    private void InitializeFirebase()
    {
        Debug.Log("Setting up Firebase Auth and Database");
        //Set the authentication instance object
        auth = FirebaseAuth.DefaultInstance;
        //Set the database instance object
        dbReference = FirebaseDatabase.DefaultInstance.RootReference;
    }

    //Function for the login button
    public void LoginButton()
    {
        //Call the login coroutine passing the email and password
        StartCoroutine(Login(emailLoginField.text, passwordLoginField.text));
    }

    //Function for the register button
    public void RegisterButton()
    {
        //Call the register coroutine passing the email, password, and username
        StartCoroutine(Register(emailRegisterField.text, passwordRegisterField.text, usernameRegisterField.text));
    }

    private IEnumerator Login(string _email, string _password)
    {
        //Call the Firebase auth sign-in function passing the email and password
        Task<AuthResult> LoginTask = auth.SignInWithEmailAndPasswordAsync(_email, _password);
        //Wait until the task completes
        yield return new WaitUntil(predicate: () => LoginTask.IsCompleted);

        if (LoginTask.Exception != null)
        {
            //If there are errors, handle them
            Debug.LogWarning(message: $"Failed to login task with {LoginTask.Exception}");
            FirebaseException firebaseEx = LoginTask.Exception.GetBaseException() as FirebaseException;
            AuthError errorCode = (AuthError)firebaseEx.ErrorCode;

            string message = "Login Failed!";
            switch (errorCode)
            {
                case AuthError.MissingEmail:
                    message = "Missing Email";
                    break;
                case AuthError.MissingPassword:
                    message = "Missing Password";
                    break;
                case AuthError.WrongPassword:
                    message = "Wrong Password";
                    break;
                case AuthError.InvalidEmail:
                    message = "Invalid Email";
                    break;
                case AuthError.UserNotFound:
                    message = "Account does not exist";
                    break;
            }
            warningLoginText.text = message;
        }
        else
        {
            //User is now logged in
            User = LoginTask.Result.User;
            Debug.LogFormat("User signed in successfully: {0} ({1})", User.DisplayName, User.Email);
            warningLoginText.text = "";
            confirmLoginText.text = "Logged In";
        }
    }

    private IEnumerator Register(string _email, string _password, string _username)
    {
        if (_username == "")
        {
            //If the username field is blank, show a warning
            warningRegisterText.text = "Missing Username";
        }
        else if (passwordRegisterField.text != passwordRegisterVerifyField.text)
        {
            //If the passwords do not match, show a warning
            warningRegisterText.text = "Password Does Not Match!";
        }
        else
        {
            //Call the Firebase auth sign-in function passing the email and password
            Task<AuthResult> RegisterTask = auth.CreateUserWithEmailAndPasswordAsync(_email, _password);
            //Wait until the task completes
            yield return new WaitUntil(predicate: () => RegisterTask.IsCompleted);

            if (RegisterTask.Exception != null)
            {
                //If there are errors, handle them
                Debug.LogWarning(message: $"Failed to register task with {RegisterTask.Exception}");
                FirebaseException firebaseEx = RegisterTask.Exception.GetBaseException() as FirebaseException;
                AuthError errorCode = (AuthError)firebaseEx.ErrorCode;

                string message = "Register Failed!";
                switch (errorCode)
                {
                    case AuthError.MissingEmail:
                        message = "Missing Email";
                        break;
                    case AuthError.MissingPassword:
                        message = "Missing Password";
                        break;
                    case AuthError.WeakPassword:
                        message = "Weak Password";
                        break;
                    case AuthError.EmailAlreadyInUse:
                        message = "Email Already In Use";
                        break;
                }
                warningRegisterText.text = message;
            }
            else
            {
                //User has now been created
                User = RegisterTask.Result.User;

                if (User != null)
                {
                    //Create a user profile and set the username
                    UserProfile profile = new UserProfile { DisplayName = _username };

                    //Call the Firebase auth update user profile function passing the profile with the username
                    Task ProfileTask = User.UpdateUserProfileAsync(profile);
                    yield return new WaitUntil(predicate: () => ProfileTask.IsCompleted);

                    if (ProfileTask.Exception != null)
                    {
                        //If there are errors, handle them
                        Debug.LogWarning(message: $"Failed to register task with {ProfileTask.Exception}");
                        warningRegisterText.text = "Username Set Failed!";
                    }
                    else
                    {
                        // Create a UserStats object with default values
                        UserStats newUserStats = new UserStats(healthyScale: 0, aesthetics: 0, timeMadeForFood: 0, difficulty: 0);

                        // Save user data and stats
                        StartCoroutine(SaveUserData(_username, User.UserId, _password, newUserStats));
                        warningRegisterText.text = "";
                        UIManager.instance.LoginScreen();
                    }
                }
            }
        }
    }

    private IEnumerator SaveUserData(string username, string uid, string password, UserStats userStats)
    {
        // User information dictionary
        var userInformation = new Dictionary<string, object>
        {
            { "username", username },
            { "uid", uid },
            { "password", password }, // Hash this in production
            { "time_created", ServerValue.Timestamp }
        };

        // Save user information to the 'Players' node using the username as the key
        var infoTask = dbReference.Child("Players").Child(username).SetValueAsync(userInformation);
        yield return new WaitUntil(() => infoTask.IsCompleted);

        if (infoTask.Exception != null)
        {
            Debug.LogWarning($"Failed to save user information: {infoTask.Exception}");
        }
        else
        {
            Debug.Log("User information saved successfully.");
        }

        // Save user stats using the UserStats class instance
        var statsTask = dbReference.Child("Stats").Child(username).SetValueAsync(userStats.ToDictionary());
        yield return new WaitUntil(() => statsTask.IsCompleted);

        if (statsTask.Exception != null)
        {
            Debug.LogWarning($"Failed to save user stats: {statsTask.Exception}");
        }
        else
        {
            Debug.Log("User stats saved successfully.");
        }
    }
}

// UserStats class
[System.Serializable]
public class UserStats
{
    public int healthyScale;
    public int aesthetics;
    public int timeMadeForFood;
    public int difficulty;

    public UserStats(int healthyScale = 0, int aesthetics = 0, int timeMadeForFood = 0, int difficulty = 0)
    {
        this.healthyScale = healthyScale;
        this.aesthetics = aesthetics;
        this.timeMadeForFood = timeMadeForFood;
        this.difficulty = difficulty;
    }

    // Convert the stats to a dictionary
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "healthy_scale", healthyScale },
            { "aesthetics", aesthetics },
            { "time_made_for_food", timeMadeForFood },
            { "difficulty", difficulty }
        };
    }
}
