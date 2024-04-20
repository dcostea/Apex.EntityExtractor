using LLama;
using LLama.Common;
using Microsoft.AspNetCore.Mvc;
using LLama.Native;

namespace Apex.EntityExtractor.Controllers;

[ApiController]
[Route("[controller]")]
public class LlamaSharpExtractorController : ControllerBase
{
    //private const string LlavaModelPath = @"c:\temp\LLMs\vicuna-13b-q6_k.gguf";
    //private const string MMProjModelPath = @"c:\temp\LLMs\mmproj-vicuna13b-f16-q6_k.gguf";

    //private const string LlavaModelPath = @"c:\temp\LLMs\llava-v1.6-mistral-7b.Q8_0.gguf";
    //private const string MMProjModelPath = @"c:\temp\LLMs\mmproj-mistral7b-f16.gguf";

    private const string LlavaModelPath = @"c:\temp\LLMs\llava-v1.6-mistral-7b.Q5_K_M.gguf";
    private const string MMProjModelPath = @"c:\temp\LLMs\mmproj-mistral7b-f16-q6_k.gguf";

    //private const string LlavaModelPath = @"c:\temp\LLMs\moondream2-text-model.Q5_K_M.gguf";
    //private const string MMProjModelPath = @"c:\temp\LLMs\moondream2-mmproj-f16.gguf";

    [HttpGet("llava")]
    public async Task<IActionResult> GetLlamaSharpLlavaCompletionAsync(string? imagePath = @"c:\Temp\Receipt.jpg")
    {
        imagePath ??= @"c:\Temp\Receipt.jpg";

        NativeLibraryConfig.Instance.WithCuda(true);

        NativeLogConfig.llama_log_set((level, message) =>
        {
            if (message.StartsWith("llama_") ||
                message.StartsWith("llm_") ||
                message.StartsWith("clip_") ||
                message.StartsWith('.')
                )
                return;
            Console.WriteLine(message.TrimEnd('\n'));
        });
        
        var parameters = new ModelParams(LlavaModelPath)
        {
            ContextSize = 4096,
            Seed = 1337,
            GpuLayerCount = 5,
        };

        using var model = LLamaWeights.LoadFromFile(parameters);
        using var context = model.CreateContext(parameters);
        using var clipModel = LLavaWeights.LoadFromFile(MMProjModelPath);

        var executor = new InteractiveExecutor(context, clipModel)
        {
            ImagePaths = [imagePath]
        };
        var session = new ChatSession(executor);

        var inferenceParams = new InferenceParams
        {
            Temperature = 0.1f,
            AntiPrompts = ["\nUSER:"],
            MaxTokens = 1024,
            //RepeatPenalty = 1.15f
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Maximum tokens: {inferenceParams.MaxTokens} and the context size is {parameters.ContextSize}.");
        
        // this can help with few-shot learning with images
        ////var hnd = SafeLlavaImageEmbedHandle.CreateFromFileName(clipModel.NativeHandle, context, imagePath);
        ////int _pastTokensCount = 0;
        ////clipModel.EvalImageEmbed(context, hnd, ref _pastTokensCount);

        var systemPrompt = """
            You're an assistant designed to transcribe text from images attached by the user.
            Stick to the visible text in the image.
            """;
        //var systemPrompt = """
        //    As an assistant, your primary function is to extract text from images.
        //    When a user attaches an image, your task is to accurately transcribe the text visible in the image.
        //    It’s crucial that you do not infer, interpret, or invent any text.
        //    Your responses should strictly adhere to the text that is clearly readable within the image.
        //    """;

        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System, systemPrompt);

        var prompt = """
            <image>
            Extract the text into a JSON object:
            [{
            """;
        ////var prompt = $$"""
        ////<{{imagePath}}>
        ////USER:
        ////You're an assistant designed to extract entities.
        ////User will attach an image and you'll respond with named entities you've extracted into a valid JSON object.

        ////Check again if all the entities are readable in the attached image, if they are not there, remove the entity from JSON response.
        ////Include only the entities that can be visually located in the image.

        ////### Output example
        ////{ 
        ////  "Phone": "123-4343222",
        ////  "Location": "Time Square 23221",
        ////  "City": "New Yotk",
        ////  "Country": "USA",
        ////  "Total": "22.21",
        ////  "Customer": "Don Joe"
        ////}

        ////ASSISTANT:
        ////""";

        //var prompt = $$"""
        //<{{imagePath}}>
        //USER:
        //Include only the entities that can be read in the attached image.

        //ASSISTANT:
        //""";

        do
        {
            chatHistory.AddMessage(AuthorRole.User, prompt);

            Console.ForegroundColor = Spectre.Console.Color.White;

            await foreach (var text in session.ChatAsync(chatHistory, inferenceParams))
            {
                Console.Write(text);
            }

            Console.WriteLine();
            prompt = Console.ReadLine();
            Console.ForegroundColor = ConsoleColor.Green;

            // let the user finish with /bye
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Equals("/bye", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Bye!");
                break;
            }

            Console.WriteLine(prompt);
        }
        while (true);

        return Ok();
    }
}
