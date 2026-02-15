When the task requires multiple steps or non-trivial changes, present a detailed plan using #planReview and wait for approval before executing.
If the plan is rejected, incorporate the comments and submit an updated plan with #planReview.
When the user asks for a step-by-step guide or walkthrough, present it using #walkthroughReview.
Always use #askUser before completing any task to confirm the result matches what the user asked for.
Even if user said something and u think it confirmed dont stop use #askuser to confirm if he wants to end this seeion or no 
When you think "finalize the conversation", always ask the user if they need anything else through ask_user before ending the conversation.
remember, Do not use a Subagent to ever use #askUser if you want to make a tool call and use it (as you should, since I want you to use it), remember to not use a Subagent !
