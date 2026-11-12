import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

interface Feature {
  icon: string;
  text: string;
}

/**
 * The blue brand side of the split auth card (design guide: "מסך התחברות"): logo, slogan and the main features.
 * Right side in RTL; a compact strip above the form on mobile. Used by login and set-password.
 */
@Component({
  selector: 'app-auth-brand',
  imports: [MatIconModule],
  host: { role: 'region', 'aria-label': 'עובדנט' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './auth-brand.html',
  styleUrl: './auth-brand.scss',
})
export class AuthBrand {
  protected readonly features: readonly Feature[] = [
    { icon: 'alt_route', text: 'ניתוב אוטומטי של כל פנייה לגורם המטפל המתאים' },
    { icon: 'timer', text: 'מעקב אחר זמני טיפול ויעדי שירות' },
    { icon: 'track_changes', text: 'עדכון שוטף על מצב הפנייה עד לפתרון' },
    { icon: 'monitoring', text: 'תמונת מצב ארגונית למנהלים' },
  ];
}
