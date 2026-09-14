export interface DeathCauseCopy {
  readonly label: string;
  readonly explanation: string;
}

export function deathCauseCopy(cause: number): DeathCauseCopy {
  switch (cause) {
    case 1: return { label: "Reserve exhaustion", explanation: "Stored energy reached the terminal floor." };
    case 2: return { label: "Structural failure", explanation: "Cell structure fell below the viable minimum." };
    case 3: return { label: "Senescence", explanation: "Age-related mortality was the recorded trigger." };
    case 4: return { label: "Temperature exposure", explanation: "Temperature crossed this DNA’s lethal tolerance." };
    case 5: return { label: "Hydrogen sulfide exposure", explanation: "Hydrogen sulfide crossed this DNA’s lethal tolerance." };
    case 6: return { label: "Sulfur dioxide exposure", explanation: "Sulfur dioxide crossed this DNA’s lethal tolerance." };
    case 7: return { label: "Maintenance failure", explanation: "The organism could not fund essential maintenance." };
    default: return { label: "Other recorded cause", explanation: `Mechanism ${cause} is not recognized by this client.` };
  }
}
