export type Step = {
  participants: string[];
  attendees: string[];
  recipientIssues: { name: string; choices: { id: string; label: string; emails: string[] }[] }[];
  reminderMinutes: number | null;
  id: string;
  agent: string;
  title: string;
  detail: string;
  status: string;
  result: string | null;
  name: string;
  action: string;
  folderId: string;
  folderName: string;
  start: string;
  end: string;
  includeLinks: boolean;
  createMeet: boolean;
  meetUrl: string | null;
  resultUrl: string | null;
};

export type Workflow = {
  language: string;
  timeZone: string;
  id: string;
  prompt: string;
  mode: string;
  source: string;
  createdAt: string;
  status: string;
  steps: Step[];
};

export type Connection = { id: string; name: string; connected: boolean };

export type Project = {
  id: string;
  name: string;
  description: string;
  deadline: string;
  tasks: { id: string; title: string; done: boolean }[];
  draft: string;
};

export type Billing = {
  plan: string;
  developmentAccess: boolean;
  simulationAccess: boolean;
  checkoutEnabled: boolean;
  trialAccess?: boolean;
  trialAvailable?: boolean;
  trialEndsAt?: string;
  trialRemaining?: number;
  plansRemaining?: number;
  monthlyPrice: number;
  currency: string;
};
