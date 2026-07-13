using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI; // for UI Image

public class LoadingSpinnerExample : MonoBehaviour
{
    public Image spinnerImage; // assign a spinner image in Inspector

    private bool isLoading = false;

    async void Start()
    {
        // Start loading process
        isLoading = true;
        
        // Launch multiple async "fake loading" tasks
        // Task task1 = FakeLoadingTask(2f, "Loading Player Data...");
        // Task task2 = FakeLoadingTask(3f, "Loading Enemy Assets...");
        // Task task3 = FakeLoadingTask(1.5f, "Loading World...");
        
        Awaitable awaitable1 = FakeLoadingAwaitable(2f, "Loading Player Data...");
        Awaitable awaitable2 = FakeLoadingAwaitable(3f, "Loading Enemy Assets...");
        Awaitable awaitable3 = FakeLoadingAwaitable(1.5f, "Loading World...");

        // Wait for all loading tasks to finish
        // await Task.WhenAll(task1, task2, task3);
        // await Awaitable.

        isLoading = false;

        Debug.Log("All loading finished!");
    }

    async Task FakeLoadingTask(float delaySeconds, string taskName)
    {
        Debug.Log($"{taskName} started.");
        await Task.Delay(Mathf.RoundToInt(delaySeconds * 1000)); // simulate loading
        Debug.Log($"{taskName} completed.");
    }
    
    async Awaitable FakeLoadingAwaitable(float delaySeconds, string taskName)
    {
        Debug.Log($"{taskName} started.");
        await Task.Delay(Mathf.RoundToInt(delaySeconds * 1000)); // simulate loading
        Debug.Log($"{taskName} completed.");
    }

    void Update()
    {
        if (isLoading)// && spinnerImage != null)
        {
            // Rotate the spinner
            Debug.Log("Update");
            // spinnerImage.transform.Rotate(0, 0, -360 * Time.deltaTime);
        }
    }
}
