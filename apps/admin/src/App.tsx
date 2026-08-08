import { PuntiroProvider, Surface } from '@puntiro/ui';
import '@puntiro/ui/styles.css';

export function App() {
  return (
    <PuntiroProvider locale="ru" mode="standard">
      <Surface>
        <main>
          <h1>Puntiro Admin</h1>
          <p>Инфраструктура административного приложения готова.</p>
        </main>
      </Surface>
    </PuntiroProvider>
  );
}
