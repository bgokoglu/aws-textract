namespace AWSTextract;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FuzzySharp;

public class TextractParser
{
    public Dictionary<string, string> ExtractFields(List<string> lines, string expectedPatient, string expectedDoctor, string expectedNDC, string expectedInvoiceDate)
    {
        var fields = new Dictionary<string, string>();

        int patientScore = 0, doctorScore = 0, ndcScore = 0, invoiceDateScore = 0;
        
        var expectedPatientAliases = GenerateNameAliases(expectedPatient);
        var expectedDoctorAliases = GenerateNameAliases(expectedDoctor);

        foreach (var line in lines)
        {
            var lowerLine = line.ToLower();

            // Fuzzy match: Patient Name from aliases
            foreach (var alias in expectedPatientAliases)
            {
                var patientMatchScore = Fuzz.PartialRatio(alias.ToLower(), lowerLine);
                if (patientMatchScore > patientScore && patientMatchScore > 70)
                {
                    patientScore = patientMatchScore;
                    fields["PatientName"] = line.Trim();
                }
            }

            // Fuzzy match: Doctor Name from aliases
            foreach (var alias in expectedDoctorAliases)
            {
                var doctorMatchScore = Fuzz.PartialRatio(alias.ToLower(), lowerLine);
                if (doctorMatchScore > doctorScore && doctorMatchScore > 70)
                {
                    doctorScore = doctorMatchScore;
                    fields["DoctorName"] = line.Trim();
                }
            }

            // Fuzzy match: NDC
            var ndcMatches = Regex.Matches(line, @"(?i)\b(?:ndc[-:\s]*)?(\d{5}-\d{3,4}-\d{2})\b");
            foreach (Match match in ndcMatches)
            {
                var foundNDC = match.Groups[1].Value;

                var score = Fuzz.Ratio(expectedNDC, foundNDC);
                if (score > ndcScore && score > 85)
                {
                    ndcScore = score;
                    fields["NDC"] = foundNDC;
                }
            }
            
            var invoiceDateMatchScore = Fuzz.PartialRatio(expectedInvoiceDate, lowerLine);
            if (invoiceDateMatchScore > invoiceDateScore && invoiceDateMatchScore > 85)
            {
                invoiceDateScore = invoiceDateMatchScore;
                fields["InvoiceDate"] = line.Trim();
            }

            // Other fields
            // if (lowerLine.Contains("hydrocodone") || lowerLine.Contains("tab"))
            //     fields["Medication"] = line.Trim();
            //
            // if (lowerLine.Contains("take") || lowerLine.Contains("every") || lowerLine.Contains("hours"))
            //     fields["DosageInstructions"] = line.Trim();
            //
            // if (lowerLine.Contains("refill") && Regex.IsMatch(line, @"\d+"))
            //     fields["Refills"] = Regex.Match(line, @"\d+").Value;
            //
            // if (lowerLine.Contains("qty") && Regex.IsMatch(line, @"\d+"))
            //     fields["Quantity"] = Regex.Match(line, @"\d+").Value;
            //
            // if (Regex.IsMatch(line, @"\d{2}-\d{2}-\d{4}"))
            //     fields["Date"] = Regex.Match(line, @"\d{2}-\d{2}-\d{4}").Value;
        }

        // Add fuzzy scores (optional for logging/debugging)
        fields["PatientScore"] = patientScore.ToString();
        fields["DoctorScore"] = doctorScore.ToString();
        fields["NDCScore"] = ndcScore.ToString();
        fields["InvoiceDateScore"] = invoiceDateScore.ToString();

        return fields;
    }
    
    public List<string> GenerateNameAliases(string fullName)
    {
        fullName = fullName?.Trim();
        var aliases = new List<string>();

        if (string.IsNullOrWhiteSpace(fullName))
            return aliases;

        // Remove unwanted punctuation from full name
        fullName = Regex.Replace(fullName, @"[.,]+$", "").Trim();

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            aliases.Add(fullName);
            return aliases;
        }

        string first = parts[0].Trim().Trim(',', '.');
        string last = parts[1].Trim().Trim(',', '.');
        
        // Always include raw full name
        aliases.Add(fullName);
        
        // If we have both names and they’re usable
        if (first.Length >= 2 && last.Length >= 2)
        {
            aliases.Add($"{first} {last}");
            aliases.Add($"{first[0]}. {last}");
            aliases.Add($"{last}, {first}");
            aliases.Add($"{last}, {first[0]}");
            aliases.Add(last);
        }
        else
        {
            // Add whatever makes sense based on what we have
            if (first.Length >= 2)
                aliases.Add(first);

            if (last.Length >= 2)
                aliases.Add(last);
        }

        return aliases.Distinct().ToList(); 
    }
}
