using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginScreen : MonoBehaviour
{
    [SerializeField]
    Button _loginButton;

    [SerializeField]
    TMP_InputField _usernameField;

    [SerializeField]
    TMP_InputField _passwordField;

    public void OnLoginButtonPress()
    {
        // Disable the button as a debounce — prevents duplicate requests
        // and gives the user immediate visual feedback.
        _loginButton.interactable = false;

        EventBus.Instance.Publish<LoginMessage, UserAuthenticatedMessage>(new LoginMessage
        {
            Username = _usernameField.text,
            Password = _passwordField.text
        }, OnLoginResponse, timeout: 10f);
    }

    protected virtual void OnLoginResponse(UserAuthenticatedMessage response)
    {
        if (!response.Success) {
            Debug.Log("Login failed");
            _loginButton.interactable = true;
            return;
        }

        Debug.Log($"Login successful — transitioning to Game scene");
        SceneManager.LoadScene("Game");
    }
}
