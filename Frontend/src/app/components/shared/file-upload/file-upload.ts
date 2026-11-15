import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  forwardRef,
  inject,
  input,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { ATTACHMENT_ACCEPT, ATTACHMENT_ICONS, ATTACHMENT_LIMITS, attachmentKind } from '../../../models/attachment.models';
import { FileSizePipe } from '../../../pipes/file-size.pipe';
import { RejectedFile, validateFiles } from '../../../services/attachment-validation';

let nextId = 0;

/**
 * Attachment picker (spec 7.4, 6.3): a "choose files" button (keyboard) and a drop zone (mouse), up to 3 files of 5MB,
 * pdf/jpg/png/docx – checked here with the server's messages, so the employee knows before sending. The content check
 * stays on the server; its error is shown through serverError.
 * A form control with a File[] value: <app-file-upload formControlName="files" [serverError]="filesError()" />
 */
@Component({
  selector: 'app-file-upload',
  imports: [MatButtonModule, MatIconModule, FileSizePipe],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => FileUpload), multi: true }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './file-upload.html',
  styleUrl: './file-upload.scss',
})
export class FileUpload implements ControlValueAccessor {
  /** The group's visible label. */
  readonly label = input('קבצים מצורפים (רשות)');
  /** An error about the files from the server (400 files / files[i], 413), shown with the local rejections. */
  readonly serverError = input<string | null>(null);
  /** Locks the picker without disabling the control (e.g. while the form is being sent). */
  readonly busy = input(false);

  private readonly injector = inject(Injector);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  // read: ElementRef – the buttons are mat-button hosts.
  private readonly chooseButton = viewChild.required<string, ElementRef<HTMLButtonElement>>('chooseButton', {
    read: ElementRef,
  });
  private readonly removeButtons = viewChildren<string, ElementRef<HTMLButtonElement>>('removeButton', {
    read: ElementRef,
  });

  protected readonly id = `file-upload-${nextId++}`;
  protected readonly accept = ATTACHMENT_ACCEPT;
  protected readonly maxFiles = ATTACHMENT_LIMITS.maxFiles;

  protected readonly files = signal<readonly File[]>([]);
  protected readonly rejected = signal<readonly RejectedFile[]>([]);
  private readonly disabledByForm = signal(false);
  protected readonly disabled = computed(() => this.disabledByForm() || this.busy());
  protected readonly dragging = signal(false);
  /** "Choose" was used with 3 files already chosen. */
  protected readonly fullNotice = signal(false);
  protected readonly full = computed(() => this.files().length >= this.maxFiles);

  private onChange: (value: File[]) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: readonly File[] | null): void {
    this.files.set(value ?? []);
    // A reset of the form (after sending) clears the old messages too.
    if (!value?.length) {
      this.rejected.set([]);
      this.fullNotice.set(false);
    }
  }

  registerOnChange(fn: (value: File[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabledByForm.set(disabled);
  }

  /** Moves the focus to the picker – e.g. when the server rejected a file. */
  focus(): void {
    this.chooseButton().nativeElement.focus();
  }

  protected icon(file: File): string {
    return ATTACHMENT_ICONS[attachmentKind(file.name)];
  }

  protected openPicker(): void {
    if (this.disabled()) {
      return;
    }
    if (this.full()) {
      this.rejected.set([]);
      this.fullNotice.set(true);
      return;
    }
    this.fileInput().nativeElement.click();
  }

  protected onPicked(): void {
    const input = this.fileInput().nativeElement;
    this.add(Array.from(input.files ?? []));
    // The same file can be chosen again after it was removed.
    input.value = '';
  }

  protected onDragOver(event: DragEvent): void {
    if (this.disabled() || !hasFiles(event)) {
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'copy';
    }
    this.dragging.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    const zone = event.currentTarget as HTMLElement;
    if (!(event.relatedTarget instanceof Node) || !zone.contains(event.relatedTarget)) {
      this.dragging.set(false);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    if (this.disabled()) {
      return;
    }
    this.add(Array.from(event.dataTransfer?.files ?? []));
  }

  protected remove(index: number): void {
    const files = this.files().filter((_, i) => i !== index);
    this.files.set(files);
    this.fullNotice.set(false);
    this.onChange([...files]);
    this.onTouched();
    // The focus stays in the list (the next file's button), or goes back to "choose".
    afterNextRender(
      () => {
        const buttons = this.removeButtons();
        const next = buttons[Math.min(index, buttons.length - 1)];
        (next ?? this.chooseButton()).nativeElement.focus();
      },
      { injector: this.injector },
    );
  }

  private add(incoming: File[]): void {
    if (incoming.length === 0) {
      return;
    }
    const { accepted, rejected } = validateFiles(this.files(), incoming);
    this.rejected.set(rejected);
    this.fullNotice.set(false);
    if (accepted.length > 0) {
      const files = [...this.files(), ...accepted];
      this.files.set(files);
      this.onChange([...files]);
    }
    this.onTouched();
  }
}

/** Dragging text or a link is not a file drop. */
function hasFiles(event: DragEvent): boolean {
  return Array.from(event.dataTransfer?.types ?? []).includes('Files');
}
