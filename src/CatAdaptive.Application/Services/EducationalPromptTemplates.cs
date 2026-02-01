namespace CatAdaptive.Application.Services;

/// <summary>
/// Prompt templates for educational content generation.
/// </summary>
public static class EducationalPromptTemplates
{
    /// <summary>
    /// 5E Model explanation template.
    /// </summary>
    public const string Explanation5E = @"Generate an explanation using the 5E instructional model:

TOPIC: {topic}
STUDENT LEVEL: {level}
PRIOR KNOWLEDGE: {priorKnowledge}
KNOWN GAPS: {gaps}
PREFERRED MODALITY: {modality}

Structure your response as follows:

## ENGAGE
Create interest with a clinical scenario, intriguing question, or real-world connection.

## EXPLORE
Connect to student's existing knowledge and experiences. Build bridges from what they know.

## EXPLAIN
Provide clear, accurate information with:
- One relevant analogy or metaphor
- Key terminology defined
- Step-by-step logical progression

## ELABORATE
Add depth with:
- Two concrete examples
- Application to clinical practice
- Address this common misconception: {misconception}

## EVALUATE
Include a quick self-assessment question to check understanding.

Keep the explanation focused, engaging, and appropriate for the student's level.";

    /// <summary>
    /// Bloom's taxonomy question generation template.
    /// </summary>
    public const string QuestionGeneration = @"Generate assessment questions using Bloom's Taxonomy:

TOPIC: {topic}
CURRENT MASTERY: {masteryLevel}
STUDENT HISTORY: {history}
IDENTIFIED GAPS: {gaps}

Generate 2 questions for each cognitive level:

### REMEMBER (Level 1)
- Test basic recall of facts and definitions
- Include clear right/wrong answer

### UNDERSTAND (Level 2)
- Require explanation in student's own words
- Test comprehension of concepts

### APPLY (Level 3)
- Present new situations requiring knowledge application
- Include realistic clinical scenarios

### ANALYZE (Level 4)
- Require breaking down components and relationships
- Include comparison or contrast elements

### EVALUATE (Level 5)
- Require making judgments based on criteria
- Include decision-making scenarios

### CREATE (Level 6)
- Require producing something new
- Include design or synthesis tasks

For each question, provide:
1. Clear question stem
2. Context if needed
3. Expected answer or rubric
4. Suggested time (minutes)
5. Hint for struggling students

Return as structured JSON array.";

    /// <summary>
    /// Adaptive feedback using sandwich method.
    /// </summary>
    public const string AdaptiveFeedback = @"Provide personalized feedback using the sandwich method:

STUDENT RESPONSE: {response}
EXPECTED ANSWER: {expected}
RESPONSE TIME: {responseTime} seconds
ATTEMPT NUMBER: {attempts}
CONFIDENCE LEVEL: {confidence}

Structure your feedback as:

### POSITIVE (Specific Praise)
- Acknowledge specific correct elements
- Recognize effort, improvement, or good reasoning
- Be genuine and specific

### CONSTRUCTIVE (Targeted Help)
- Identify the specific misconception or gap
- Provide a targeted hint (not the answer)
- Suggest a specific learning strategy
- Reference relevant prior knowledge

### POSITIVE (Encouragement)
- Include a growth mindset message
- Provide clear, actionable next step
- Suggest a relevant resource if applicable

Keep feedback:
- Concise (under 200 words)
- Actionable
- Encouraging
- Specific to this student's response";

    /// <summary>
    /// Tree of Thoughts content generation.
    /// </summary>
    public const string TreeOfThoughts = @"Generate personalized learning content using Tree of Thoughts with 4 expert perspectives:

LEARNING OBJECTIVE: {objective}
STUDENT PROFILE:
- Current Mastery: {masteryLevel}
- Strengths: {strengths}
- Gaps: {gaps}
- Preferred Modality: {modality}
- Confidence Level: {confidence}

Each expert proposes an approach:

### EXPERT 1: Instructional Designer
Focus on learning structure, sequencing, and cognitive load management.
Approach: [Propose approach]

### EXPERT 2: Subject Matter Expert
Focus on accuracy, depth, and clinical relevance.
Approach: [Propose approach]

### EXPERT 3: Learning Scientist
Focus on evidence-based learning techniques and retention.
Approach: [Propose approach]

### EXPERT 4: Student Advocate
Focus on engagement, motivation, and accessibility.
Approach: [Propose approach]

### SYNTHESIS
Combine the best elements from each expert into a cohesive content plan:
1. Hook/Engagement element
2. Core content structure
3. Practice opportunities
4. Assessment strategy

### FINAL CONTENT
Generate the actual learning content based on the synthesized approach.";

    /// <summary>
    /// ReAct framework for adaptive content.
    /// </summary>
    public const string ReActAdaptive = @"Adapt content based on student interaction using ReAct:

CURRENT SITUATION:
- Student Response: {response}
- Time Taken: {responseTime} seconds
- Confidence: {confidence}
- Previous Attempts: {attempts}

CONTENT CONTEXT:
- Current Topic: {topic}
- Last Explanation: {lastExplanation}
- Available Resources: {resources}

Execute the following reasoning chain:

THOUGHT 1: Analyze student's understanding level
What does the response reveal about their understanding?
ACTION 1: Evaluate response accuracy and identify misconceptions
OBSERVATION 1: [Analysis results]

THOUGHT 2: Identify specific learning needs
What type of support would be most effective?
ACTION 2: Determine if remediation, advancement, or clarification is needed
OBSERVATION 2: [Learning need identification]

THOUGHT 3: Select content adaptation strategy
Which strategy best addresses the identified need?
ACTION 3: Choose from: simplify, add examples, change modality, provide scaffold, advance
OBSERVATION 3: [Strategy selection]

THOUGHT 4: Generate adapted content
Create personalized content based on the selected strategy
ACTION 4: Generate the adapted explanation or next learning step
OBSERVATION 4: [Generated content]

THOUGHT 5: Plan follow-up assessment
How can we verify the adaptation was effective?
ACTION 5: Design a quick check for understanding
OBSERVATION 5: [Assessment design]

### ADAPTED CONTENT
[Provide the personalized content]

### NEXT STEPS
[Provide recommended next actions]";

    /// <summary>
    /// Clinical case generation template.
    /// </summary>
    public const string ClinicalCaseGeneration = @"Generate a clinical case study for learning:

TOPIC: {topic}
LEARNING OBJECTIVES: {objectives}
DIFFICULTY: {difficulty}
TARGET BLOOM'S LEVEL: {bloomsLevel}

Create a realistic patient case with the following structure:

### PATIENT PRESENTATION
- Demographics (age, gender, relevant background)
- Chief complaint
- History of present illness
- Relevant past medical history
- Current medications

### CLINICAL DATA
- Vital signs
- Physical exam findings
- Relevant lab results or imaging

### DECISION POINTS
Include 3-4 decision points where the student must:
1. Identify the key clinical issue
2. Determine appropriate next steps
3. Apply pharmacological knowledge
4. Consider patient-specific factors

### QUESTIONS
For each decision point, provide:
- The question
- Expected reasoning process
- Correct answer with rationale
- Common errors to watch for

### LEARNING OUTCOMES
Summarize key takeaways and connections to the learning objectives.";

    /// <summary>
    /// Mnemonic generation template.
    /// </summary>
    public const string MnemonicGeneration = @"Create memorable learning aids for:

TOPIC: {topic}
KEY POINTS TO REMEMBER:
{keyPoints}

Generate the following memory aids:

### ACRONYM
Create an acronym where each letter represents a key concept.
- Make it pronounceable
- Make it relevant to the topic
- Include an explanation of each letter

### ACROSTIC
Create a sentence or phrase where each word starts with a letter representing key information.
- Make it memorable and meaningful
- Connect to clinical practice if possible

### VISUAL ASSOCIATION
Describe a vivid mental image that links the concepts together.
- Use concrete, colorful imagery
- Connect to familiar objects or scenarios

### STORY/NARRATIVE
Create a short story (2-3 sentences) that incorporates all key points.
- Make it engaging
- Make it logical
- Make it easy to visualize

### USAGE TIP
Provide a brief tip on how to best use these mnemonics for learning.";
}
