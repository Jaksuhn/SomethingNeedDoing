using SomethingNeedDoing.Core.Interfaces;
using System.Net.Http;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Core;

/// <summary>
/// Represents an HTTP dependency.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpDependency"/> class.
/// </remarks>
/// <param name="httpClient">The HTTP client.</param>
/// <param name="url">The URL of the dependency.</param>
/// <param name="name">The name of the dependency.</param>
public class HttpDependency(HttpClient httpClient, string url, string name) : IMacroDependency
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc/>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public DependencyType Type => DependencyType.Remote;

    /// <inheritdoc/>
    public string Source { get; } = url ?? throw new ArgumentNullException(nameof(url));

    /// <inheritdoc/>
    public async Task<string> GetContentAsync()
    {
        var response = await _httpClient.GetAsync(Source);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(Source);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<DependencyValidationResult> ValidateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(Source);
            if (!response.IsSuccessStatusCode)
                return DependencyValidationResult.Failure($"HTTP request failed with status code: {response.StatusCode}");

            // Try to read the content to validate it
            await response.Content.ReadAsStringAsync();
            return DependencyValidationResult.Success();
        }
        catch (Exception ex)
        {
            return DependencyValidationResult.Failure($"Error validating HTTP dependency: {ex.Message}");
        }
    }
}
