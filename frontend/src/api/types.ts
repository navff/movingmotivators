export type SurveyType = 'moving_motivators' | 'health_check'
export type SurveyStatus = 'draft' | 'open' | 'closed'

export interface User {
  id: string
  email: string
  displayName: string | null
  isAdmin: boolean
  consentVersion: string | null
  consentAt: string | null
  needsOnboarding: boolean
  currentConsentVersion: string
}

export interface MotivatorCardInfo {
  key: string
  name: string
  englishName?: string | null
  statement: string
  accentColor: string
  discussionPrompts?: string[]
  /** Описания оценок 1–5 звёзд; нет в опросах методики v1. */
  levels?: string[] | null
}

export interface HealthCheckCategoryInfo {
  key: string
  name: string
  awesome: string
  awful: string
  discussionPrompts?: string[]
}

export interface Catalog {
  version: number
  showEnglishCardNames: boolean
  motivators: MotivatorCardInfo[]
  healthCheckCategories: HealthCheckCategoryInfo[]
}

export interface Pricing {
  freeMaxParticipants: number
  surveyPriceRub: number
}

export interface SiteInfo {
  productName: string
  baseUrl: string
  legal: { name: string; shortName: string; inn: string; ogrnip: string; address: string; email: string }
}

export interface LastSurvey {
  id: string
  title: string
  type: SurveyType
  status: SurveyStatus
  createdAt: string
}

export interface Team {
  id: string
  name: string
  size: number
  archivedAt: string | null
  createdAt: string
  surveyCount: number
  openSurveyCount: number
  lastSurvey?: LastSurvey | null
}

export interface SurveyListItem {
  id: string
  type: SurveyType
  title: string
  status: SurveyStatus
  isAnonymous: boolean
  declaredParticipants: number
  priceRub: number
  responseCount: number
  createdAt: string
  closesAt: string | null
  closedAt: string | null
  checkoutRequired: boolean
}

export interface TeamDetails {
  id: string
  name: string
  size: number
  archivedAt: string | null
  createdAt: string
  surveys: SurveyListItem[]
}

export interface SurveyChange {
  id: string
  text: string
}

export interface Survey {
  id: string
  teamId: string
  teamName: string
  type: SurveyType
  title: string
  isAnonymous: boolean
  declaredParticipants: number
  priceRub: number
  publicCode: string
  publicUrl: string
  welcomeMessage?: string | null
  settings: { changes: SurveyChange[]; categories: string[] }
  autoCloseOnFull: boolean
  status: SurveyStatus
  openedAt?: string | null
  closesAt?: string | null
  closedAt?: string | null
  createdAt: string
  responseCount: number
  responsesOverDeclared: boolean
  checkoutRequired: boolean
  canReopen: boolean
}

export interface CreateSurveyResponse {
  survey: Survey
  checkoutRequired: boolean
  priceRub: number
}

export interface CheckoutSummary {
  surveyId: string
  surveyType: SurveyType
  surveyTitle: string
  teamName: string
  participants: number
  amountRub: number
  email: string
  legalName: string
  legalInn: string
}

export type PaymentMethod = 'card' | 'sbp' | 'sberpay'

// ---------- results ----------

export interface DiscussionPrompt {
  priority: 'high' | 'normal'
  kind: string
  text: string
  cardKey?: string
  changeId?: string
  questions?: string[]
}

export interface CommentItem {
  text: string
  name?: string
  submittedOn?: string
  categoryKey?: string
  cardKey?: string
}

export interface MotivatorCardStats {
  key: string
  avgRank: number
  stdRank: number
  consensus: 'unanimous' | 'mixed' | 'divided'
  distribution: number[]
  /** Насколько развито в команде, 1..5; нет в опросах методики v1. */
  avgRating?: number
  ratingDistribution?: number[]
}

export interface ChangeCardImpact {
  key: string
  up: number
  neutral: number
  down: number
  index: number
}

export interface ChangeImpactResult {
  id: string
  text: string
  responseCount: number
  cards: ChangeCardImpact[]
  overall: number
}

export type ImpactMarks = Record<string, Record<string, number>>

export interface MotivatorsParticipant {
  name: string
  ranking: string[]
  impact?: ImpactMarks | null
  comment?: string | null
  ratings?: Record<string, number> | null
}

export interface MotivatorsResult {
  type: 'moving_motivators'
  responseCount: number
  cards: MotivatorCardStats[]
  top3: string[]
  bottom3: string[]
  changes: ChangeImpactResult[]
  mostPainfulChangeId?: string
  mostPositiveChangeId?: string
  comments: CommentItem[]
  prompts: DiscussionPrompt[]
  participants?: MotivatorsParticipant[]
  ratingComments?: CommentItem[]
}

export type TrafficColor = 'green' | 'yellow' | 'red'

export interface HealthCheckAnswer {
  score: number
  trend: number
  comment?: string | null
}

export interface HealthCheckCategoryStats {
  key: string
  green: number
  yellow: number
  red: number
  avgScore?: number
  color?: TrafficColor
  trendUp: number
  trendSame: number
  trendDown: number
  trend?: number
  comments: CommentItem[]
}

export interface HealthCheckResult {
  type: 'health_check'
  responseCount: number
  categories: HealthCheckCategoryStats[]
  overallAvg?: number
  focus: string[]
  comments: CommentItem[]
  prompts: DiscussionPrompt[]
  participants?: { name: string; answers: Record<string, HealthCheckAnswer>; comment?: string | null }[]
}

export type SurveyResult = MotivatorsResult | HealthCheckResult

export interface HealthCheckHistory {
  categoryKeys: string[]
  waves: {
    surveyId: string
    title: string
    closedAt?: string
    responseCount: number
    overallAvg?: number
    categories: Record<string, { avgScore?: number; color?: TrafficColor; trend?: number }>
  }[]
}

export interface MotivatorsComparison {
  a: { id: string; title: string; closedAt?: string; responseCount: number }
  b: { id: string; title: string; closedAt?: string; responseCount: number }
  cards: { key: string; avgA: number; avgB: number; delta: number }[]
  participants?: { name: string; shift: Record<string, number> }[]
}

// ---------- public ----------

export interface PublicSurvey {
  code: string
  status: 'open' | 'closed'
  type?: SurveyType
  title?: string
  teamName?: string
  organizerName?: string
  welcomeMessage?: string
  isAnonymous?: boolean
  closesAt?: string
  cards?: MotivatorCardInfo[]
  changes?: SurveyChange[]
  categories?: HealthCheckCategoryInfo[]
  consentVersion?: string
}

export interface MyResponse {
  participantName?: string
  payload: { ranking?: string[]; ratings?: Record<string, number> | null; ratingComments?: Record<string, string> | null; impact?: ImpactMarks | null; answers?: Record<string, HealthCheckAnswer> }
  comment?: string
  updatedCount: number
}

// ---------- admin ----------

export interface AdminStats {
  from: string
  to: string
  totalUsers: number
  totalTeams: number
  totalSurveys: number
  totalResponses: number
  surveysOverDeclared: number
  days: { day: string; registrations: number; surveys: number; responses: number }[]
}

export interface AdminFunnel {
  from: string
  to: string
  freeSurveys: number
  paidRequiredSurveys: number
  checkoutViewed: number
  paymentMethodSelected: number
  paymentClicked: number
  checkoutAbandoned: number
  conversionFromDraft: number
  conversionFromCheckout: number
  byMethod: { key: string; count: number }[]
  bySize: { key: string; count: number }[]
}

export interface AdminUser {
  id: string
  email: string
  displayName?: string
  createdAt: string
  lastLoginAt?: string
  teams: number
  surveys: number
}
