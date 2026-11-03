import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

@Component({
  selector: 'app-group-order-page',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DatePipe, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  // Завантажуємо дані з перегляду (або через сервіс/сигнал)
  readonly orderData = signal(GROUP_ORDER_PREVIEW);

  // Витягуємо invite
  readonly invite = computed(() => this.orderData().invite);
  
  // поки що статичні дані; на рівні 2 їх замінить відповідь API
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
  
  // Витягуємо Grand Total із sessions.total
  readonly grandTotal = computed(() => this.orderData().sessions.total);
}
