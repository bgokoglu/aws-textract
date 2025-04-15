// See https://aka.ms/new-console-template for more information

/*
 *	Use this code snippet in your app.
 *	If you need more information about configurations or implementing the sample code, visit the AWS docs:
 *	https://aws.amazon.com/developer/language/net/getting-started
 */

using System.Text.RegularExpressions;
using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using AWSTextract;
using S3Object = Amazon.Rekognition.Model.S3Object;

// await Rekognition();
await Textract();

async Task Textract()
{
    var fileName = "InvoiceFile2.pdf"; // Replace with your local file path
    var bucketName = "eit-lab-rekognition-test";
    
    var textractConfig  = new AmazonTextractConfig
    {
        RegionEndpoint = RegionEndpoint.USEast2,
        Profile = new Profile("itg-lab")
    };
    var textractClient = new AmazonTextractClient(textractConfig);

    byte[] imageBytes = await File.ReadAllBytesAsync(fileName);

    // var request = new DetectDocumentTextRequest
    // {
    //     Document = new Document
    //     {
    //         Bytes = new MemoryStream(imageBytes)
    //     }
    // };
    
    var request = new DetectDocumentTextRequest
    {
        Document = new Document
        {
            S3Object = new Amazon.Textract.Model.S3Object
            {
                Bucket = bucketName,
                Name = fileName
            }
        }
    };

    try
    {
        var response = await textractClient.DetectDocumentTextAsync(request);

        var lines = new List<string>();
        foreach (var block in response.Blocks)
        {
            if (block.BlockType == "LINE")
            {
                lines.Add(block.Text);
                Console.WriteLine($"Line: {block.Text}");
            }
        }
        
        var parser = new TextractParser();
        var extractedFields = parser.ExtractFields(
            lines,
            expectedPatient: "K. Brabender",
            expectedDoctor: "Kyle Eggerman",
            expectedNDC: "59148-0114-80",
            expectedInvoiceDate: "04/02/2025"
        );

        Console.WriteLine("\n=== Extracted Fields ===");
        foreach (var kvp in extractedFields)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }

        int score = (
            int.Parse(extractedFields["PatientScore"]) +
            int.Parse(extractedFields["DoctorScore"]) +
            int.Parse(extractedFields["NDCScore"]) +
            int.Parse(extractedFields["InvoiceDateScore"])
        ) / 4;

        Console.WriteLine($"\n🚦 Final Confidence Score: {score}%");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

async Task Rekognition()
{
    var bucket = "eit-lab-rekognition-test";

// text based
// var photo = "invoice1.png";
// var fname = "Noah";
// var lname = "Bennett";
// var rx = "1122334";
// var lotNum = "ADS1424A";
// var presc_fname = "Claire";
// var presc_lname = "Foster";
// var ndc = "16729-0267-01";

// text based
// var photo = "invoice2.png";
// var fname = "Isabella";
// var lname = "Torres";
// var rx = "4455667";
// var lotNum = "AES1924A";
// var presc_fname = "Henry";
// var presc_lname = "Sullivan";
// var ndc = "0641-6159-25";

// no ndc
// var photo = "invoice3.png";
// var fname = "Olivia";
// var lname = "Wilson";
// var rx = "110010111";
// var lotNum = "AES1924A";
// var presc_fname = "Jane";
// var presc_lname = "Smith";
// var ndc = "0641-6159-25";

// full
    var photo = "invoice4.png";
    var fname = "Francis";
    var lname = "Watts";
    var rx = "93472342555";
    var lotNum = "AES1924A";
    var presc_fname = "Stephen";
    var presc_lname = "Tobman";
    var ndc = "9385772-03885-89";

// Step 1: Define the weights as before
    var weights = new Dictionary<string, float>
    {
        { "ndc", 0.2f },
        { "prescriber_name", 0.25f },
        { "rx", 0.3f },
        { "lotNum", 0.05f },
        { "patient_name", 0.2f }
    };

// Step 2: Create a dictionary to collect confidence scores
    var detectedScores = new Dictionary<string, float>
    {
        { "ndc", 0f },
        { "prescriber_name", 0f },
        { "rx", 0f },
        { "lotNum", 0f },
        { "patient_name", 0f }
    };

// Step 3: Initialize a list to keep track of low-confidence fields
    var lowConfidenceFields = new List<string>();

// var accessKeyId = "***YOUR_ACCESS_KEY_ID***";
// var secretAccessKey = "***YOUR_SECRET_ACCESS_KEY***";
// var regionEndpoint = RegionEndpoint.USEast2;
// var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
// var rekognitionClient = new AmazonRekognitionClient(credentials, regionEndpoint);

    var rekognitionClient = new AmazonRekognitionClient(new AmazonRekognitionConfig
    {
        RegionEndpoint = RegionEndpoint.USEast2,
        Profile = new Profile("itg-lab")
    });

    var detectTextRequest = new DetectTextRequest
    {
        Image = new Image
        {
            S3Object = new Amazon.Rekognition.Model.S3Object
            {
                Name = photo,
                Bucket = bucket
            }
        }
    };

    try
    {
        var detectTextResponse = await rekognitionClient.DetectTextAsync(detectTextRequest);
        Console.WriteLine($"Detected lines and words for {photo}");
        Console.WriteLine("**********************************");

        foreach (var text in detectTextResponse.TextDetections)
        {
            Console.WriteLine($"Detected: {text.DetectedText} - {text.Type} - {text.Confidence}");

            // Prescriber Name Handling
            var prescPattern = $@"\b({presc_fname[0]}\.\s*|{presc_fname}\s+[A-Z]?\.?\s*){presc_lname}\b";
            if (Regex.IsMatch(text.DetectedText, prescPattern, RegexOptions.IgnoreCase))
            {
                Console.WriteLine($"Prescriber name (Regex) found: {text.Confidence:F2}%");
                detectedScores["prescriber_name"] = Math.Max(detectedScores["prescriber_name"], text.Confidence);
            }
            else if (text.DetectedText.Contains($"{presc_fname} {presc_lname}", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Prescriber name (Full match) found: {text.Confidence:F2}%");
                detectedScores["prescriber_name"] = Math.Max(detectedScores["prescriber_name"], text.Confidence);
            }
            else if (string.Equals(text.DetectedText, presc_fname, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Prescriber first name found: {text.Confidence:F2}%");
                detectedScores["prescriber_name"] = Math.Max(detectedScores["prescriber_name"], text.Confidence);
            }
            else if (string.Equals(text.DetectedText, presc_lname, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Prescriber last name found: {text.Confidence:F2}%");
                detectedScores["prescriber_name"] = Math.Max(detectedScores["prescriber_name"], text.Confidence);
            }

            // Patient Name Handling
            var patientPattern = $@"\b({fname[0]}\.\s*|{fname}\s+[A-Z]?\.?\s*){lname}\b";
            if (Regex.IsMatch(text.DetectedText, patientPattern, RegexOptions.IgnoreCase))
            {
                Console.WriteLine($"Patient name (Regex) found: {text.Confidence:F2}%");
                detectedScores["patient_name"] = Math.Max(detectedScores["patient_name"], text.Confidence);
            }
            else if (text.DetectedText.Contains($"{fname} {lname}", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Patient name (Full match) found: {text.Confidence:F2}%");
                detectedScores["patient_name"] = Math.Max(detectedScores["patient_name"], text.Confidence);
            }
            else if (string.Equals(text.DetectedText, fname, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Patient first name found: {text.Confidence:F2}%");
                detectedScores["patient_name"] = Math.Max(detectedScores["patient_name"], text.Confidence);
            }
            else if (string.Equals(text.DetectedText, lname, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Patient last name found: {text.Confidence:F2}%");
                detectedScores["patient_name"] = Math.Max(detectedScores["patient_name"], text.Confidence);
            }

            // Other Fields
            if (text.DetectedText.Contains(rx, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"RX found: {text.Confidence:F2}%");
                detectedScores["rx"] = Math.Max(detectedScores["rx"], text.Confidence);
            }

            if (string.Equals(text.DetectedText, lotNum, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Lot Number found: {text.Confidence:F2}%");
                detectedScores["lotNum"] = Math.Max(detectedScores["lotNum"], text.Confidence);
            }

            if (text.DetectedText.Contains(ndc, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"NDC found: {text.Confidence:F2}%");
                detectedScores["ndc"] = Math.Max(detectedScores["ndc"], text.Confidence);
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }

// Step 4: Compute the overall confidence score (no need for totalWeight)
    var overallConfidence = detectedScores.Sum(field => field.Value * weights[field.Key]);

// Step 5: Check if the confidence is low (below 90%)
    if (overallConfidence < 90f)
    {
        // Step 6: Identify which fields had low confidence
        foreach (var field in detectedScores)
        {
            if (field.Value < 90f)
                lowConfidenceFields.Add(field.Key);
        }

        // Output the low-confidence message
        Console.WriteLine($"WARNING: Confidence score is low: {overallConfidence:F2}%");
        Console.WriteLine("The following fields contributed to the low confidence score:");
    
        foreach (var lowField in lowConfidenceFields)
        {
            Console.WriteLine($"- {lowField} (Confidence: {detectedScores[lowField]:F2}%)");
        }
    }
    else
    {
        Console.WriteLine($"Overall Document Confidence Score: {overallConfidence:F2}%");
    }
}


// string secretName = "eit-nonprod-ecs-hosting-shionogi-user-db-password";
// string region = "us-east-2";
//
// IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
//
// GetSecretValueRequest request = new GetSecretValueRequest
// {
//     SecretId = secretName,
//     VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
// };
//
// GetSecretValueResponse response;
//
// try
// {
//     response = await client.GetSecretValueAsync(request);
// }
// catch (Exception e)
// {
//     // For a list of the exceptions thrown, see
//     // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
//     throw e;
// }
//
// string secret = response.SecretString;
//
// Console.WriteLine($"Secret: {secret}");

// using System.Data.SqlTypes;
//
// var reportStartDate = DateTime.Today.Date.AddDays(5).AddDays(-7);
// var reportEndDate = DateTime.Today.Date.AddDays(5);
//
// Console.WriteLine($"Start date: {reportStartDate}");
// Console.WriteLine($"End date: {reportEndDate}");
//
//
// var reportStartDateUtc = TimeZoneHelper.ConvertCentralStdTimeToUtc(reportStartDate);
// var reportEndDateUtc = TimeZoneHelper.ConvertCentralStdTimeToUtc(reportEndDate);
//
// Console.WriteLine($"StartDate : {reportStartDateUtc}"); 
// Console.WriteLine($"EndDate : {reportEndDateUtc}");
//
//
// Console.WriteLine(TimeZoneHelper.ConvertToUtc(reportStartDate, "Central Standard Time"));
//
// public static class TimeZoneHelper
// {
//     public static DateTime ConvertToUtc(DateTime dateTime, string timeZoneId)
//     {
//         var localDateTime = dateTime.Kind == DateTimeKind.Unspecified
//             ? DateTime.SpecifyKind(dateTime, DateTimeKind.Local) // If unspecified, set it to Local
//             : dateTime;
//
//         if (localDateTime.Kind == DateTimeKind.Local)
//         {
//             var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
//             localDateTime = TimeZoneInfo.ConvertTime(localDateTime, timeZone);
//         }
//
//         return TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
//     }
//
//     public static DateTime ConvertFromUtc(DateTime dateTime, string timeZoneId)
//     {
//         return TimeZoneInfo.ConvertTimeFromUtc(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
//     }
//
//     public static DateTime? ConvertCentralStdTimeToUtc(DateTime? dateTime)
//     {
//         if (!dateTime.HasValue)
//             return null;
//
//         // Convert to UTC using the adjusted CST time
//         return ConvertToUtc(dateTime.Value, "Central Standard Time");
//     }
// }