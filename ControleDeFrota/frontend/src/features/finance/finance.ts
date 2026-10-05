import type { StatusOption } from '../../components/common';

export type PaymentStatus = 'Pending' | 'Scheduled' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Cancelled';
export type PaymentMethod = 'BankTransfer' | 'Pix' | 'CreditCard' | 'DebitCard' | 'Cash' | 'FleetCard' | 'DirectDebit' | 'Invoice' | 'Other';
export type ExpenseFrequency = 'Monthly' | 'Quarterly' | 'Semiannual' | 'Annual';
export type BudgetStatus = 'UnderBudget' | 'NearBudget' | 'OverBudget' | 'NoBudget';

export const PAYMENT_STATUS: Record<PaymentStatus, StatusOption> = {
  Pending: { label: 'Pendente', color: 'gray' },
  Scheduled: { label: 'Agendado', color: 'blue' },
  PartiallyPaid: { label: 'Parcialmente pago', color: 'orange' },
  Paid: { label: 'Pago', color: 'teal' },
  Overdue: { label: 'Atrasado', color: 'red' },
  Cancelled: { label: 'Cancelado', color: 'gray' },
};

export const PAYMENT_METHOD: Record<PaymentMethod, { label: string }> = {
  BankTransfer: { label: 'Transferência bancária' },
  Pix: { label: 'PIX' },
  CreditCard: { label: 'Cartão de crédito' },
  DebitCard: { label: 'Cartão de débito' },
  Cash: { label: 'Dinheiro' },
  FleetCard: { label: 'Cartão frota' },
  DirectDebit: { label: 'Débito automático' },
  Invoice: { label: 'Faturado' },
  Other: { label: 'Outro' },
};

export const EXPENSE_FREQUENCY: Record<ExpenseFrequency, { label: string }> = {
  Monthly: { label: 'Mensal' },
  Quarterly: { label: 'Trimestral' },
  Semiannual: { label: 'Semestral' },
  Annual: { label: 'Anual' },
};

export const BUDGET_STATUS: Record<BudgetStatus, StatusOption> = {
  UnderBudget: { label: 'Dentro do orçamento', color: 'teal' },
  NearBudget: { label: 'Próximo do limite', color: 'orange' },
  OverBudget: { label: 'Acima do orçamento', color: 'red' },
  NoBudget: { label: 'Sem orçamento', color: 'gray' },
};

export interface CostCenter {
  id: string;
  code: string;
  name: string;
  description: string | null;
  parentCostCenterId: string | null;
  parentCostCenterName: string | null;
  isActive: boolean;
}

export interface ExpenseCategory {
  id: string;
  name: string;
  code: string;
  description: string | null;
  parentCategoryId: string | null;
  parentCategoryName: string | null;
  isActive: boolean;
  isSystemCategory: boolean;
}

export interface Expense {
  id: string;
  expenseCategoryId: string;
  categoryName: string;
  costCenterId: string | null;
  costCenterName: string | null;
  vehicleId: string | null;
  licensePlate: string | null;
  driverId: string | null;
  driverName: string | null;
  workshopId: string | null;
  workshopName: string | null;
  supplierName: string | null;
  description: string;
  referenceNumber: string | null;
  expenseDate: string;
  dueDate: string | null;
  amount: number | null;
  paidAmount: number | null;
  paymentDate: string | null;
  paymentMethod: PaymentMethod | null;
  status: PaymentStatus;
  isRecurring: boolean;
  recurringExpenseId: string | null;
  notes: string | null;
  isDuplicateSuspect: boolean;
  canEdit: boolean;
  canCancel: boolean;
  canRegisterPayment: boolean;
  cancelledAt: string | null;
  cancellationReason: string | null;
}

export interface ExpenseRequest {
  expenseCategoryId: string | null;
  costCenterId: string | null;
  vehicleId: string | null;
  driverId: string | null;
  workshopId: string | null;
  supplierName: string | null;
  description: string;
  referenceNumber: string | null;
  expenseDate: string | null;
  dueDate: string | null;
  amount: number | null;
  paymentMethod: PaymentMethod | null;
  notes: string | null;
  fileIds?: string[];
}

export interface RecurringExpense {
  id: string;
  description: string;
  expenseCategoryId: string;
  categoryName: string;
  costCenterId: string | null;
  costCenterName: string | null;
  vehicleId: string | null;
  licensePlate: string | null;
  workshopId: string | null;
  workshopName: string | null;
  supplierName: string | null;
  amount: number;
  paymentMethod: PaymentMethod | null;
  frequency: ExpenseFrequency;
  startDate: string;
  endDate: string | null;
  dueDayOfMonth: number;
  isActive: boolean;
  nextDueDate: string | null;
  lastGeneratedDueDate: string | null;
}

export interface Budget {
  id: string;
  year: number;
  month: number | null;
  expenseCategoryId: string;
  categoryName: string;
  costCenterId: string | null;
  costCenterName: string | null;
  vehicleId: string | null;
  licensePlate: string | null;
  amount: number;
  notes: string | null;
}

export interface BudgetVsActual {
  budget: Budget;
  actual: number | null;
  remaining: number | null;
  utilizationPercent: number | null;
  status: BudgetStatus;
  isPartial: boolean;
}

export interface CategoryCost {
  expenseCategoryId: string | null;
  categoryName: string;
  amount: number;
}

export interface CostCenterCost {
  costCenterId: string | null;
  costCenterName: string;
  amount: number;
}

export interface MonthlyCost {
  year: number;
  month: number;
  amount: number;
}

export interface FinanceDashboard {
  totalFleetCostYtd: number | null;
  monthlyCost: number | null;
  fleetCostPerKm: number | null;
  costPerKmHasSufficientData: boolean;
  fuelCost: number | null;
  maintenanceCost: number | null;
  tireCost: number | null;
  otherCost: number | null;
  overdueExpenseCount: number;
  overdueExpenseAmount: number | null;
  dueSoonRecurringCount: number;
  monthlyEvolution: MonthlyCost[];
  costByCategory: CategoryCost[];
  isPartial: boolean;
  canSeeCosts: boolean;
}

export interface VehicleCostBreakdown {
  vehicleId: string;
  fuelCost: number;
  maintenanceCost: number;
  tireCost: number;
  otherCost: number;
  totalCost: number;
  isPartial: boolean;
}

export interface VehicleCostPerKm {
  vehicleId: string;
  totalCost: number;
  distanceKm: number | null;
  costPerKm: number | null;
  hasSufficientData: boolean;
  isPartial: boolean;
}

export interface VehicleTco {
  vehicleId: string;
  acquisitionValue: number | null;
  sinceDate: string;
  operatingCost: number;
  totalCost: number;
  costPerMonth: number;
  distanceKm: number | null;
  costPerKm: number | null;
  hasSufficientMileageData: boolean;
  fuelCost: number;
  maintenanceCost: number;
  tireCost: number;
  otherCost: number;
  isPartial: boolean;
}

export interface VehicleRankingRow {
  vehicleId: string;
  licensePlate: string;
  model: string;
  totalCost: number;
  costPerKm: number | null;
  fuelCost: number;
  maintenanceCost: number;
  tireCost: number;
  expenseCount: number;
}
