import { fakeFile } from '../components/employee/testing/ticket-fixtures';
import { FILE_REJECTION_MESSAGES, validateFiles } from './attachment-validation';

describe('validateFiles', () => {
  const MB = 1024 * 1024;

  it('accepts the allowed types, whatever the case of the extension', () => {
    const files = [fakeFile('תלוש.PDF'), fakeFile('צילום.JPEG'), fakeFile('מסך.png')];
    const result = validateFiles([], files);
    expect(result.accepted).toEqual(files);
    expect(result.rejected).toEqual([]);
  });

  it('rejects a file over 5MB, with the server\'s message', () => {
    const result = validateFiles([], [fakeFile('גדול.pdf', 6 * MB)]);
    expect(result.accepted).toEqual([]);
    expect(result.rejected).toEqual([{ fileName: 'גדול.pdf', reason: 'גודל כל קובץ: עד 5MB' }]);
  });

  it('accepts exactly 5MB', () => {
    expect(validateFiles([], [fakeFile('a.pdf', 5 * MB)]).accepted.length).toBe(1);
  });

  it('rejects a type that is not allowed (and a name without an extension)', () => {
    const result = validateFiles([], [fakeFile('setup.exe'), fakeFile('README'), fakeFile('doc.pdf.exe')]);
    expect(result.accepted).toEqual([]);
    expect(result.rejected.map((r) => r.reason)).toEqual([
      FILE_REJECTION_MESSAGES.typeNotAllowed,
      FILE_REJECTION_MESSAGES.typeNotAllowed,
      FILE_REJECTION_MESSAGES.typeNotAllowed,
    ]);
  });

  it('rejects an empty file', () => {
    expect(validateFiles([], [fakeFile('a.pdf', 0)]).rejected[0].reason).toBe('הקובץ ריק');
  });

  it('4 files: the first 3 are kept, the 4th is rejected', () => {
    const files = [fakeFile('1.pdf'), fakeFile('2.pdf'), fakeFile('3.pdf'), fakeFile('4.pdf')];
    const result = validateFiles([], files);
    expect(result.accepted).toEqual(files.slice(0, 3));
    expect(result.rejected).toEqual([{ fileName: '4.pdf', reason: 'ניתן לצרף עד 3 קבצים' }]);
  });

  it('counts the files already chosen', () => {
    const result = validateFiles([fakeFile('1.pdf'), fakeFile('2.pdf')], [fakeFile('3.pdf'), fakeFile('4.pdf')]);
    expect(result.accepted.map((f) => f.name)).toEqual(['3.pdf']);
    expect(result.rejected.map((r) => r.fileName)).toEqual(['4.pdf']);
  });

  it('a rejected file does not take a place', () => {
    const result = validateFiles([fakeFile('1.pdf'), fakeFile('2.pdf')], [fakeFile('x.exe'), fakeFile('3.pdf')]);
    expect(result.accepted.map((f) => f.name)).toEqual(['3.pdf']);
  });

  it('rejects the same file chosen twice', () => {
    const file = fakeFile('a.pdf');
    const result = validateFiles([file], [file]);
    expect(result.rejected).toEqual([{ fileName: 'a.pdf', reason: 'הקובץ כבר נבחר' }]);
  });
});
