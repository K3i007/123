"use client";
/* eslint-disable @next/next/no-img-element -- image URLs will be supplied by the Phase 5 media service. */

import { useEffect, useRef, useState } from "react";

export interface GalleryImage {
  url: string;
  alt: string;
}

export function VehicleGallery({ images, title }: { images: GalleryImage[]; title: string }) {
  const allImages = images.length
    ? images
    : [{ url: "/vehicle-placeholder.png", alt: `Imagen provisional de ${title}` }];
  const [selected, setSelected] = useState(0);
  const [open, setOpen] = useState(false);
  const opener = useRef<HTMLButtonElement>(null);
  const dialog = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    dialog.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        return;
      }
      if (event.key !== "Tab" || !dialog.current) return;
      const focusable = dialog.current.querySelectorAll<HTMLElement>(
        'button, [href], [tabindex]:not([tabindex="-1"])',
      );
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (!first || !last) return;
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      }
      if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open]);
  const close = () => {
    setOpen(false);
    opener.current?.focus();
  };
  const current = allImages[selected] ?? allImages[0];
  return (
    <>
      <section aria-label="Galería del vehículo" className="space-y-3">
        <button
          ref={opener}
          type="button"
          onClick={() => setOpen(true)}
          className="block w-full overflow-hidden rounded-lg border border-slate-200 bg-white focus:outline-none"
        >
          <img
            src={current.url}
            alt={current.alt}
            className="aspect-[16/10] w-full object-cover"
            loading="eager"
          />
        </button>
        {allImages.length > 1 && (
          <div className="flex gap-2 overflow-x-auto">
            {allImages.map((image, index) => (
              <button
                type="button"
                key={image.url}
                onClick={() => setSelected(index)}
                aria-label={`Ver imagen ${index + 1}`}
                aria-current={index === selected}
                className="shrink-0 rounded border border-slate-300 p-1 aria-[current=true]:border-brand"
              >
                <img src={image.url} alt="" className="h-16 w-24 object-cover" />
              </button>
            ))}
          </div>
        )}
      </section>
      {open && (
        <div
          className="fixed inset-0 z-50 grid place-items-center bg-slate-950/80 p-4"
          role="presentation"
        >
          <div
            ref={dialog}
            role="dialog"
            aria-modal="true"
            aria-label={`Imagen ampliada de ${title}`}
            tabIndex={-1}
            className="relative max-h-full w-full max-w-5xl rounded-lg bg-white p-3 outline-none"
          >
            <button
              type="button"
              onClick={close}
              className="absolute right-5 top-5 z-10 rounded bg-white px-3 py-2 text-sm font-semibold shadow"
            >
              Cerrar
            </button>
            <img
              src={current.url}
              alt={current.alt}
              className="max-h-[80vh] w-full object-contain"
            />
          </div>
        </div>
      )}
    </>
  );
}
