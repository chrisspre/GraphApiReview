✅ Strategy: Separate Logic from Presentation
To make your CLI tool both testable and flexible, structure it so that:

Core logic produces a well-defined data structure with all possible output sections. And where most of them are optional
Output rendering is handled separately—either as:

Human-readable text (default) in the same way as it is now
Structured JSON format (via a --json flag)

the integration tests then can focus mostly on presense and absence of certain sections.